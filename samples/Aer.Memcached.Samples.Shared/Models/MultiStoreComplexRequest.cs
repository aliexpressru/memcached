namespace Aer.Memcached.Samples.Shared.Models;

public class MultiStoreComplexRequest
{
    public Dictionary<string, ComplexModel> KeyValues { get; set; }
    
    public DateTimeOffset ExpirationTime { get; set; }
    
    public Dictionary<string, DateTimeOffset?> ExpirationMapWithDateTimeOffset { get; set; }
    
    public Dictionary<string, TimeSpan?> ExpirationMapWithDateTimeSpan { get; set; }
}