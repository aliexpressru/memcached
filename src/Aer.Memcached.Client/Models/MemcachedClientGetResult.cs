using Aer.Memcached.Client.Interfaces;

namespace Aer.Memcached.Client.Models;

public class MemcachedClientGetResult<T>: IMemcachedClientResult
{
    public static MemcachedClientGetResult<T> Unsuccessful { get; } = new()
    {
        Success = false,
        Result = default
    };

    public T Result { get; set; }
    
    public bool Success { get; set; }
}