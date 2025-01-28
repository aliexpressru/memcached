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
}