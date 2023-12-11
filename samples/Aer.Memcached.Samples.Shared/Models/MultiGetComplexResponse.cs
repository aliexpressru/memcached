namespace Aer.Memcached.Samples.Shared.Models;

public class MultiGetComplexResponse
{
    public IDictionary<string, ComplexModel> KeyValues { get; set; }
}