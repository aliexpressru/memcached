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
    private readonly MemcachedConfiguration _config;
    private readonly ILogger<CacheSynchronizer> _logger;

    private readonly ICollection<MemcachedConfiguration.SyncServer> _syncServers;

    public CacheSynchronizer(
        ISyncServersProvider syncServersProvider, 
        ICacheSyncClient cacheSyncClient,
        IOptions<MemcachedConfiguration> config,
        ILogger<CacheSynchronizer> logger)
    {
        _syncServersProvider = syncServersProvider;
        _cacheSyncClient = cacheSyncClient;
        _config = config.Value;
        _logger = logger;

        _syncServers = _syncServersProvider.GetSyncServers();
    }

    public async Task SyncCache<T>(CacheSyncModel<T> model, CancellationToken token)
    {
        try
        {
            if (_syncServersProvider.IsConfigured())
            {
                var source = new CancellationTokenSource(_config.SyncSettings.TimeToSync);
                var syncCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(token, source.Token);
                
                await Parallel.ForEachAsync(
                    _syncServers,
                    new ParallelOptions()
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount
                    },
                    async (syncServer, _) =>
                    {
                        await _cacheSyncClient.Sync(syncServer, model, syncCancellationToken.Token);
                    });
            }
        }
        catch (Exception e) // no need to crash if something goes wrong with sync
        {
            _logger.LogError(e, "Unable to sync cache");
        }
    }
}