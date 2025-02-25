namespace Aer.Memcached.Samples.Shared.Models;

public class MultiStoreRequest
{
    public Dictionary<string, string> KeyValues { get; set; }
    
    public DateTimeOffset? ExpirationTime { get; set; }

    public TimeSpan? TimeSpan { get; set; }
    
    public Dictionary<string, DateTimeOffset?> ExpirationMapWithDateTimeOffset { get; set; }
    
    public Dictionary<string, TimeSpan?> ExpirationMapWithDateTimeSpan { get; set; }
}