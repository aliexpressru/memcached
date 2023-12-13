namespace Aer.Memcached.Client.Models;

public class MemcachedClientResult
{
    /// <summary>
    /// If set to <c>true</c>, then no errors occured on memcached side.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// If any errors occured on memcached side, this property contains the error message.
    /// </summary>
    public string ErrorMessage { get; }

    public static MemcachedClientResult Successful { get; } = new(success: true);
    
    internal MemcachedClientResult(bool success, string errorMessage = null)
    {
        Success = success;
        ErrorMessage = errorMessage;
    }

    public static MemcachedClientResult Unsuccessful(string errorMessage)
    {
        return new MemcachedClientResult(success: false, errorMessage);
    }
    
    
}