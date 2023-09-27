namespace Aer.Memcached.Client.Models;

public class MemcachedClientValueResult<T>
{
    public static MemcachedClientValueResult<T> Unsuccessful { get; } = new()
    {
        Success = false,
        Result = default
    };

    public T Result { get; set; }
    
    /// <summary>
    /// No errors occured on memcached side
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// true - if no value is stored
    /// default value is true as command to memcached can be unsuccessful
    /// </summary>
    public bool IsEmptyResult { get; set; } = true;
}