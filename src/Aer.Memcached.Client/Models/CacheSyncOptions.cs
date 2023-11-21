namespace Aer.Memcached.Client.Models;

public class CacheSyncOptions
{
    /// <summary>
    /// For manual switching off of sync in registered endpoint
    /// Allows to avoid recursive restoring of cache
    /// </summary>
    public bool IsManualSyncOn { get; set; } = true;
    
    /// <summary>
    /// For write-through scenarios when we are sure that cache must be stored on all clusters
    /// avoiding cache sync window intervals that protects us from multiple cache sync restoring
    /// of the same key values
    /// </summary>
    public bool ForceUpdate { get; set; }
}