using Aer.Memcached.Client.Config;

namespace Aer.Memcached.Client.Interfaces;

public interface ICacheSynchronizer
{
    bool IsSyncOn();
    
    Task SyncCache(
        Dictionary<string, object> keyValues,
        DateTimeOffset? expirationTime,
        CancellationToken token
    );

    void UpdateSyncServers(ICollection<MemcachedConfiguration.SyncServer> servers);
}