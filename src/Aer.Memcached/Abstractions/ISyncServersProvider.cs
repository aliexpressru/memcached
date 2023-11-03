using Aer.Memcached.Client.Config;

namespace Aer.Memcached.Abstractions;

public interface ISyncServersProvider
{
    MemcachedConfiguration.SyncServer[] GetSyncServers();
}