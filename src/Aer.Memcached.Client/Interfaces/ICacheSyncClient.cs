using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Interfaces;

public interface ICacheSyncClient
{
    Task Sync<T>(
        MemcachedConfiguration.SyncServer syncServer,
        CacheSyncModel<T> data,
        CancellationToken token);
}