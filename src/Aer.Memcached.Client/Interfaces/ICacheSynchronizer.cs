using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Interfaces;

public interface ICacheSynchronizer
{
    Task SyncCache<T>(CacheSyncModel<T> model, CancellationToken token);
}