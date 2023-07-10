using Aer.Memcached.Client.Interfaces;

namespace Aer.Memcached.Client.Models;

public class MemcachedClientResult
{
    public static MemcachedClientResult Unsuccessful { get; } = new()
    {
        Success = false
    };
    
    public static MemcachedClientResult Successful { get; } = new()
    {
        Success = true
    };

    public bool Success { get; set; }
}