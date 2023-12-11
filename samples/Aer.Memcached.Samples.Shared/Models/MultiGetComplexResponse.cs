namespace Aer.Memcached.Shared.Models;

public class MultiGetComplexResponse
{
    public IDictionary<string, ComplexModel> KeyValues { get; set; }
}