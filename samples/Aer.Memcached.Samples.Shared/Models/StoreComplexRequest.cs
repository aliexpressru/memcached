namespace Aer.Memcached.Samples.Shared.Models;

public class StoreComplexRequest
{
    public string Key { get; set; }

    public Dictionary<ComplexDictionaryKey, ComplexModel> DataToStore { get; set; }
    
    public TimeSpan? ExpirationTime { get; set; }
}