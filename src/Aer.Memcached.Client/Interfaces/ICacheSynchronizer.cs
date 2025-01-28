using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Interfaces;

public interface ICacheSynchronizer
{
    /// <summary>
    /// Determines whether the cache sync logic is enabled.
    /// </summary>
    bool IsCacheSyncEnabled();
    
    /// <summary>
    /// Syncs data to the servers that are specified in <see cref="MemcachedConfiguration.SynchronizationSettings"/>.
    /// </summary>
    /// <param name="model">Data to sync.</param>
    /// <param name="token">Cancellation token.</param>
    Task<bool> TrySyncCacheAsync(CacheSyncModel model, CancellationToken token);

    /// <summary>
    /// Deletes data on the servers that are specified in <see cref="MemcachedConfiguration.SynchronizationSettings"/>.
    /// </summary>
    /// <param name="keys">Keys to delete.</param>
    /// <param name="token">Cancellation token.</param>
    Task<bool> TryDeleteCacheAsync(IEnumerable<string> keys, CancellationToken token);
}