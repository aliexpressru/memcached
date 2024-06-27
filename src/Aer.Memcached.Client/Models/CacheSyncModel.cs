namespace Aer.Memcached.Client.Models;

/// <summary>
/// Represents a multi-cluster cache synchronization key-value DTO. 
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
public class CacheSyncModel<T>
{
    /// <summary>
    /// Gets or sets the key-values to sync.
    /// </summary>
    public IDictionary<string, T> KeyValues { get; set; }
    
    /// <summary>
    /// Gets or sets the key-value items expiration time.
    /// </summary>
    public DateTimeOffset? ExpirationTime { get; set; }
}