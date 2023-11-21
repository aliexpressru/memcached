using System.Collections.Concurrent;

namespace Aer.Memcached.Client.Models;

public class CacheSyncWindow
{
    /// <summary>
    /// Track synced key values until the time
    /// </summary>
    public DateTimeOffset OpenUntil { get; set; }
    
    /// <summary>
    /// Synced key values in the time window
    /// </summary>
    public ConcurrentDictionary<string, DateTimeOffset> SyncedKeyValues { get; set; }
}