using System.Collections.Concurrent;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Client.CacheSync;

internal class CacheSynchronizer : ICacheSynchronizer
{
    private readonly ISyncServersProvider _syncServersProvider;
    private readonly ICacheSyncClient _cacheSyncClient;
    private readonly IErrorStatisticsStore _errorStatisticsStore;
    private readonly MemcachedConfiguration _config;
    private readonly ILogger<CacheSynchronizer> _logger;

    private readonly ICollection<MemcachedConfiguration.SyncServer> _syncServers;

    private readonly ConcurrentDictionary<string, DateTimeOffset> _serverBySwitchOffTime;

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
    }

    /// <inheritdoc />
    public bool IsCacheSyncEnabled() => _syncServersProvider.IsConfigured();

    /// <inheritdoc />
    public async Task<bool> TrySyncCacheAsync(
        CacheSyncModel model,
        CancellationToken token)
    {
        if (model.KeyValues == null)
        {
            return true;
        }

        if (!IsCacheSyncEnabled())
        {
            return false;
        }

        try
        {
            var source = new CancellationTokenSource(_config.SyncSettings.TimeToSync);
            using var syncCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, source.Token);
            var utcNow = DateTimeOffset.UtcNow;

            if (model.KeyValues.Count == 0)
            {
                return true;
            }

            await Task.WhenAll(_syncServers
                .Select(syncServer =>
                    SyncData(syncServer, model, utcNow, syncCancellationTokenSource.Token)));
        }
        catch (Exception)
        {
            // this exception was already logged in _cacheSyncClient
            // no need to crash if something goes wrong with sync 
            return false;
        }

        return true;
    }

    public async Task<bool> TryDeleteCacheAsync(
        IEnumerable<string> keys,
        CancellationToken token)
    {
        if (keys == null)
        {
            return true;
        }

        if (!IsCacheSyncEnabled())
        {
            return false;
        }

        try
        {
            var source = new CancellationTokenSource(_config.SyncSettings.TimeToSync);
            using var syncCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, source.Token);
            var utcNow = DateTimeOffset.UtcNow;

            await Task.WhenAll(_syncServers
                .Select(syncServer =>
                    SyncDelete(syncServer, keys, utcNow, syncCancellationTokenSource.Token)));
        }
        catch (Exception)
        {
            // this exception was already logged in _cacheSyncClient
            // no need to crash if something goes wrong with sync
            return false;
        }

        return true;
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

    private async Task SyncData(
        MemcachedConfiguration.SyncServer syncServer,
        CacheSyncModel model,
        DateTimeOffset utcNow,
        CancellationToken token)
    {
        var serverKey = syncServer.Address;

        try
        {
            if (_serverBySwitchOffTime.TryGetValue(serverKey, out var switchOffTime) &&
                switchOffTime > utcNow)
            {
                return;
            }

            await _cacheSyncClient.SyncAsync(syncServer, model, token);
        }
        catch (Exception)
        {
            await CheckCircuitBreaker(serverKey, utcNow);

            throw;
        }
    }

    private async Task SyncDelete(
        MemcachedConfiguration.SyncServer syncServer,
        IEnumerable<string> keys,
        DateTimeOffset utcNow,
        CancellationToken token)
    {
        var serverKey = syncServer.Address;

        try
        {
            if (_serverBySwitchOffTime.TryGetValue(serverKey, out var switchOffTime) &&
                switchOffTime > utcNow)
            {
                return;
            }

            await _cacheSyncClient.DeleteAsync(syncServer, keys, token);
        }
        catch (Exception)
        {
            await CheckCircuitBreaker(serverKey, utcNow);

            throw;
        }
    }
}