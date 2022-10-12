using Aer.Memcached.Client.Interfaces;

namespace Aer.Memcached.Client.Models;

public class MemcachedClientValueResult<T>: IMemcachedClientResult
{
    public static MemcachedClientValueResult<T> Unsuccessful { get; } = new()
    {
        Success = false,
        Result = default
    };

    public T Result { get; set; }
    
    public bool Success { get; set; }
}