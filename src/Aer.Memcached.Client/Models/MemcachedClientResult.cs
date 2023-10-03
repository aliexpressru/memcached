namespace Aer.Memcached.Client.Models;

public class MemcachedClientResult
{
    public bool Success { get; set; }
    
    public static MemcachedClientResult Unsuccessful { get; } = new()
    {
        Success = false
    };
    
    public static MemcachedClientResult Successful { get; } = new()
    {
        Success = true
    };
}