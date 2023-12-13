namespace Aer.Memcached.Client.Models;

public class MemcachedClientValueResult<T>
{
    /// <summary>
    /// The result of the memcached operation.
    /// </summary>
    public T Result { get; }
    
    /// <summary>
    /// If set to <c>true</c>, then no errors occured on memcached side.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// If any errors occured on memcached side, this property contains the error message.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// <c>true</c> - if no value is stored
    /// default value is <c>true</c> as command to memcached can be unsuccessful
    /// </summary>
    public bool IsEmptyResult { get; }

    internal MemcachedClientValueResult(
        bool success,
        T result = default,
        bool isEmptyResult = true,
        string errorMessage = null)
    {
        Success = success;
        Result = result;
        ErrorMessage = errorMessage;
        IsEmptyResult = isEmptyResult;
    }

    internal static MemcachedClientValueResult<T> Successful(T result, bool isResultEmpty)
        => new(success: true, result: result, isEmptyResult: isResultEmpty);

    internal static MemcachedClientValueResult<T> Unsuccessful(string errorMessage) 
        => new(success: false, errorMessage: errorMessage);
}