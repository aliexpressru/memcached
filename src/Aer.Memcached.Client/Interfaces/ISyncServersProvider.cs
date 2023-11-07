using Aer.Memcached.Client.Config;

namespace Aer.Memcached.Client.Interfaces;

public interface ISyncServersProvider
{
    MemcachedConfiguration.SyncServer[] GetSyncServers();

    bool IsConfigured();
}