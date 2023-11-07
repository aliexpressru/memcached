using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client;

public class CacheSynchronizer : ICacheSynchronizer
{
    private readonly ISyncServersProvider _syncServersProvider;
    private readonly ICacheSyncClient _cacheSyncClient;

    private readonly ICollection<MemcachedConfiguration.SyncServer> _syncServers;

    public CacheSynchronizer(ISyncServersProvider syncServersProvider, ICacheSyncClient cacheSyncClient)
    {
        _syncServersProvider = syncServersProvider;
        _cacheSyncClient = cacheSyncClient;

        _syncServers = _syncServersProvider.GetSyncServers();
    }

    public async Task SyncCache<T>(CacheSyncModel<T> model, CancellationToken token)
    {
        if (_syncServersProvider.IsConfigured())
        {
            foreach (var syncServer in _syncServers)
            {
                await _cacheSyncClient.Sync(syncServer, model, token);
            }
        }
    }
}