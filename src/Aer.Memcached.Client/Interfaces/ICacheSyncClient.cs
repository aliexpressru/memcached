using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Interfaces;

public interface ICacheSyncClient
{
    Task SyncAsync<T>(
        MemcachedConfiguration.SyncServer syncServer,
        CacheSyncModel<T> data,
        CancellationToken token);
}