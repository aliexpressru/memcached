namespace Aer.Memcached.Client.Models;

/// <summary>
/// Represents a multi-cluster cache synchronization key-value DTO. 
/// </summary>
public class CacheSyncModel
{
    /// <summary>
    /// Gets or sets the key-values to sync.
    /// </summary>
    public IDictionary<string, byte[]> KeyValues { get; set; }
    
    /// <summary>
    /// Flags for sync data.
    /// </summary>
    public uint Flags { get; set; }
    
    /// <summary>
    /// Gets or sets the key-value items expiration time.
    /// </summary>
    public DateTimeOffset? ExpirationTime { get; set; }
    
    /// <summary>
    /// Individual key expirations that will be used instead ExpirationTime if provided.
    /// </summary>
    public IDictionary<string, DateTimeOffset?> ExpirationMap { get; set; }
    
    /// <summary>
    /// Gets or sets the batching options for cache sync operations.
    /// </summary>
    public BatchingOptions BatchingOptions { get; set; }
}