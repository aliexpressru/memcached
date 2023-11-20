using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Interfaces;

public interface ICacheSynchronizer
{
    /// <summary>
    /// Syncs data
    /// </summary>
    /// <param name="model">Data to sync</param>
    /// <param name="token">Cancellation token</param>
    Task SyncCache<T>(CacheSyncModel<T> model, CancellationToken token);
}