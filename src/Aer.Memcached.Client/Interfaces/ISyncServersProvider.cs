using Aer.Memcached.Client.Config;

namespace Aer.Memcached.Client.Interfaces;

public interface ISyncServersProvider
{
    /// <summary>
    /// Gets sync servers.
    /// </summary>
    /// <returns>Sync servers</returns>
    MemcachedConfiguration.SyncServer[] GetSyncServers();

    /// <summary>
    /// Returns <c>true</c> if sync servers provider is configured, <c>false</c> otherwise.
    /// Used to determine whether the cache sync is enabled or not. 
    /// </summary>
    bool IsConfigured();
}