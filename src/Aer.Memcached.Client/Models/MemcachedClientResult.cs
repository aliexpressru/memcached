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

    /// <summary>
    /// Gets an instance of <see cref="MemcachedClientResult"/> with a successful result.
    /// </summary>
    public static MemcachedClientResult Successful { get; } = new(success: true);
    
    internal MemcachedClientResult(bool success, string errorMessage = null)
    {
        Success = success;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Creates an instance of <see cref="MemcachedClientResult"/> with an unsuccessful result.
    /// </summary>
    /// <param name="errorMessage">The unsuccessful result error message.</param>
    public static MemcachedClientResult Unsuccessful(string errorMessage) 
        => new(success: false, errorMessage);
}