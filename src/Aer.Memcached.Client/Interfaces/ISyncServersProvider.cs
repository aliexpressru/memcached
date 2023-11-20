using Aer.Memcached.Client.Config;

namespace Aer.Memcached.Client.Interfaces;

public interface ISyncServersProvider
{
    /// <summary>
    /// Gets sync servers
    /// </summary>
    /// <returns>Sync servers</returns>
    MemcachedConfiguration.SyncServer[] GetSyncServers();

    /// <summary>
    /// Provider is configured or not
    /// </summary>
    /// <returns>Flag</returns>
    bool IsConfigured();
}