using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Interfaces;

public interface ICacheSyncClient
{
    /// <summary>
    /// Syncs cache data to the specified server
    /// </summary>
    /// <param name="syncServer">Server to sync data</param>
    /// <param name="data">Data to sync</param>
    /// <param name="token">Cancellation token</param>
    Task SyncAsync<T>(
        MemcachedConfiguration.SyncServer syncServer,
        CacheSyncModel<T> data,
        CancellationToken token);

    /// <summary>
    /// Deletes cache data on the specified server
    /// </summary>
    /// <param name="syncServer">Server to sync data</param>
    /// <param name="keys">Keys to delete</param>
    /// <param name="token">Cancellation token</param>
    Task DeleteAsync(
        MemcachedConfiguration.SyncServer syncServer,
        IEnumerable<string> keys,
        CancellationToken token);
}