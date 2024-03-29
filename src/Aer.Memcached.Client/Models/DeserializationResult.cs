namespace Aer.Memcached.Client.Models;

public class DeserializationResult<T>
{
    public T Result { get; set; }
    
    public bool IsEmpty { get; set; }

    public static DeserializationResult<T> Empty => new()
    {
        Result = default,
        IsEmpty = true
    };
}