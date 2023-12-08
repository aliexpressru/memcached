namespace Aer.Memcached.Client.Models;

public class CacheSyncOptions
{
    /// <summary>
    /// For manual switching off of sync in registered endpoint
    /// Allows to avoid recursive restoring of cache
    /// </summary>
    public bool IsManualSyncOn { get; set; } = true;
}