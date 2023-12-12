namespace Aer.Memcached.Samples.Shared.Models;

public class MultiGetResponse
{
    public IDictionary<string, string> KeyValues { get; set; }
}