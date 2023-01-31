namespace Aer.Memcached.Tests.Models;

public class RecursiveModel
{
    public long X { get; set; }
    
    public long Y { get; set; }
    
    public RecursiveModel Embedded { get; set; }
}