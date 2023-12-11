namespace Aer.Memcached.Shared.Models;

public class MultiStoreComplexRequest
{
    public Dictionary<string, ComplexModel> KeyValues { get; set; }
    
    public DateTimeOffset ExpirationTime { get; set; }
}