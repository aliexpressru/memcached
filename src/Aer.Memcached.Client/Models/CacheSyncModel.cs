namespace Aer.Memcached.Client.Models;

public class CacheSyncModel<T>
{
    public Dictionary<string, T> KeyValues { get; set; }
    
    public DateTimeOffset? ExpirationTime { get; set; }
}