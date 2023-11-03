using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;

namespace Aer.Memcached.Client;

public class CacheSynchronizer : ICacheSynchronizer
{
    private readonly MemcachedConfiguration.SynchronizationSettings _syncSettings;

    private ICollection<MemcachedConfiguration.SyncServer> _syncServers;

    public CacheSynchronizer(MemcachedConfiguration configuration)
    {
        _syncSettings = configuration.SyncSettings;
        _syncServers = Array.Empty<MemcachedConfiguration.SyncServer>();
    }

    public bool IsSyncOn() => _syncSettings != null;

    public Task SyncCache(Dictionary<string, object> keyValues, DateTimeOffset? expirationTime, CancellationToken token)
    {
        if (_syncServers.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    public void UpdateSyncServers(ICollection<MemcachedConfiguration.SyncServer> servers)
    {
        _syncServers = servers;
    }
}