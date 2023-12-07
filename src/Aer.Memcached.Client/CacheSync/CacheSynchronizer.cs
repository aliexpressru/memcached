using System.Collections.Concurrent;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Client.CacheSync;

public class CacheSynchronizer : ICacheSynchronizer
{
    private readonly ISyncServersProvider _syncServersProvider;
    private readonly ICacheSyncClient _cacheSyncClient;
    private readonly IErrorStatisticsStore _errorStatisticsStore;
    private readonly MemcachedConfiguration _config;
    private readonly ILogger<CacheSynchronizer> _logger;

    private readonly ICollection<MemcachedConfiguration.SyncServer> _syncServers;

    private readonly ConcurrentDictionary<string, DateTimeOffset> _serverBySwitchOffTime;

    private readonly SemaphoreSlim _syncWindowLocker;
    private CacheSyncWindow _syncWindow;

    public CacheSynchronizer(
        ISyncServersProvider syncServersProvider,
        ICacheSyncClient cacheSyncClient,
        IErrorStatisticsStore errorStatisticsStore,
        IOptions<MemcachedConfiguration> config,
        ILogger<CacheSynchronizer> logger)
    {
        _syncServersProvider = syncServersProvider;
        _cacheSyncClient = cacheSyncClient;
        _errorStatisticsStore = errorStatisticsStore;
        _config = config.Value;
        _logger = logger;

        _syncServers = _syncServersProvider.GetSyncServers();
        _serverBySwitchOffTime = new ConcurrentDictionary<string, DateTimeOffset>();

        _syncWindowLocker = new SemaphoreSlim(1, 1);
    }

    /// <inheritdoc />
    public async Task SyncCacheAsync<T>(
        CacheSyncModel<T> model,
        CacheSyncOptions cacheSyncOptions,
        CancellationToken token)
    {
        if (model.KeyValues == null)
        {
            return;
        }

        if (!_syncServersProvider.IsConfigured())
        {
            return;
        }

        try
        {
            var source = new CancellationTokenSource(_config.SyncSettings.TimeToSync);
            var syncCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, source.Token);
            var utcNow = DateTimeOffset.UtcNow;

            await UpdateSyncWindowAndInputKeyValues(model, cacheSyncOptions, utcNow);
            if (model.KeyValues.Count == 0)
            {
                return;
            }

            await Parallel.ForEachAsync(
                _syncServers,
                new ParallelOptions()
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = syncCancellationTokenSource.Token
                }, async (syncServer, cancellationToken) =>
                {
                    var serverKey = syncServer.Address;

                    try
                    {
                        if (_serverBySwitchOffTime.TryGetValue(serverKey, out var switchOffTime) &&
                            switchOffTime > utcNow)
                        {
                            return;
                        }

                        await _cacheSyncClient.SyncAsync(syncServer, model, cancellationToken);
                    }
                    catch (Exception)
                    {
                        await CheckCircuitBreaker(serverKey, utcNow);

                        throw;
                    }
                });

            UpdateKeyValuesInSyncWindow(model);
        }
        catch (Exception)
        {
            // no need to crash if something goes wrong with sync
        }
    }
    
    public async Task DeleteCacheAsync(
        IEnumerable<string> keys,
        CancellationToken token)
    {
        if (keys == null)
        {
            return;
        }

        if (!_syncServersProvider.IsConfigured())
        {
            return;
        }

        try
        {
            var source = new CancellationTokenSource(_config.SyncSettings.TimeToSync);
            var syncCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, source.Token);
            var utcNow = DateTimeOffset.UtcNow;

            await Parallel.ForEachAsync(
                _syncServers,
                new ParallelOptions()
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = syncCancellationTokenSource.Token
                }, async (syncServer, cancellationToken) =>
                {
                    var serverKey = syncServer.Address;

                    try
                    {
                        if (_serverBySwitchOffTime.TryGetValue(serverKey, out var switchOffTime) &&
                            switchOffTime > utcNow)
                        {
                            return;
                        }

                        await _cacheSyncClient.DeleteAsync(syncServer, keys, cancellationToken);
                    }
                    catch (Exception)
                    {
                        await CheckCircuitBreaker(serverKey, utcNow);

                        throw;
                    }
                });
        }
        catch (Exception)
        {
            // no need to crash if something goes wrong with sync
        }
    }

    private async Task UpdateSyncWindowAndInputKeyValues<T>(
        CacheSyncModel<T> model,
        CacheSyncOptions cacheSyncOptions,
        DateTimeOffset utcNow)
    {
        if (!_config.SyncSettings.CacheSyncInterval.HasValue)
        {
            return;
        }

        if (_syncWindow != null)
        {
            if (utcNow > _syncWindow.OpenUntil)
            {
                await _syncWindowLocker.WaitAsync();

                _syncWindow.OpenUntil = utcNow.Add(_config.SyncSettings.CacheSyncInterval.Value);
                _syncWindow.SyncedKeyValues = new ConcurrentDictionary<string, DateTimeOffset>();

                _syncWindowLocker.Release();
            }
            else if (cacheSyncOptions == null || cacheSyncOptions.ForceUpdate == false)
                // if force update is not passed then filter out values
                // skip it otherwise
            {
                foreach (var syncedKeyValue in _syncWindow.SyncedKeyValues)
                {
                    if (model.KeyValues.ContainsKey(syncedKeyValue.Key) &&
                        model.ExpirationTime.HasValue &&
                        model.ExpirationTime.Value.Equals(syncedKeyValue.Value))
                    {
                        model.KeyValues.Remove(syncedKeyValue.Key);
                    }
                }
            }
        }
        else
        {
            await _syncWindowLocker.WaitAsync();

            _syncWindow = new CacheSyncWindow
            {
                OpenUntil = utcNow.Add(_config.SyncSettings.CacheSyncInterval.Value),
                SyncedKeyValues = new ConcurrentDictionary<string, DateTimeOffset>()
            };

            _syncWindowLocker.Release();
        }
    }

    private void UpdateKeyValuesInSyncWindow<T>(CacheSyncModel<T> model)
    {
        if (_syncWindow == null || !model.ExpirationTime.HasValue)
        {
            return;
        }

        foreach (var inputKeyValue in model.KeyValues)
        {
            _syncWindow.SyncedKeyValues.TryAdd(inputKeyValue.Key, model.ExpirationTime.Value);
        }
    }

    private async Task CheckCircuitBreaker(string serverKey, DateTimeOffset utcNow)
    {
        if (_config.SyncSettings.CacheSyncCircuitBreaker != null)
        {
            var errorStatistics = await _errorStatisticsStore.GetErrorStatisticsAsync(serverKey,
                _config.SyncSettings.CacheSyncCircuitBreaker.MaxErrors,
                _config.SyncSettings.CacheSyncCircuitBreaker.Interval);

            if (errorStatistics.IsTooManyErrors)
            {
                _serverBySwitchOffTime.AddOrUpdate(serverKey,
                    utcNow.Add(_config.SyncSettings.CacheSyncCircuitBreaker.SwitchOffTime),
                    (_, oldValue) =>
                    {
                        if (oldValue < utcNow)
                        {
                            var newSwitchOffTime = utcNow.Add(_config.SyncSettings
                                .CacheSyncCircuitBreaker
                                .SwitchOffTime);

                            return newSwitchOffTime;
                        }

                        return oldValue;
                    });

                _logger.LogError(
                    "Sync to {SererKey} is switched off until {SwitchOffThresholdTime}, reason: too many errors",
                    serverKey,
                    _serverBySwitchOffTime[serverKey]
                );
            }
        }
    }
}