using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Interfaces;

public interface ICacheSynchronizer
{
    /// <summary>
    /// Syncs data to the servers that are specified in <see cref="MemcachedConfiguration.SynchronizationSettings"/>
    /// </summary>
    /// <param name="model">Data to sync</param>
    /// <param name="cacheSyncOptions">The options that configure cache sync</param>
    /// <param name="token">Cancellation token</param>
    Task SyncCacheAsync<T>(CacheSyncModel<T> model, CacheSyncOptions cacheSyncOptions, CancellationToken token);

    /// <summary>
    /// Deletes data on the servers that are specified in <see cref="MemcachedConfiguration.SynchronizationSettings"/>
    /// </summary>
    /// <param name="keys">Keys to delete</param>
    /// <param name="token">Cancellation token</param>
    Task DeleteCacheAsync(IEnumerable<string> keys, CancellationToken token);
}