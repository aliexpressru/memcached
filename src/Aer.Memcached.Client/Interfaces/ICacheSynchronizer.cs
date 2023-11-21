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
    Task SyncCache<T>(CacheSyncModel<T> model, CacheSyncOptions cacheSyncOptions, CancellationToken token);
}