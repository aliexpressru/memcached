using System.Diagnostics.CodeAnalysis;

namespace Aer.Memcached.Client.Models;

/// <summary>
/// Represents the result of key-value item read.
/// </summary>
/// <typeparam name="T">Type of the read value item.</typeparam>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
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
    /// If set to <c>true</c>, then the request was cancelled by external cancellation token.
    /// </summary>
    /// When equals <c>true</c>, <see cref="ErrorMessage"/> contains a name of the operation that was cancelled.
    public bool RequestCancelled { get; }

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
        string errorMessage = null,
        bool isRequestCancelled = true)
    {
        Success = success;
        Result = result;
        ErrorMessage = errorMessage;
        IsEmptyResult = isEmptyResult;
        RequestCancelled = isRequestCancelled;
    }

    /// <summary>
    /// Creates an instance of <see cref="MemcachedClientValueResult{T}"/> with a successful result.
    /// </summary>
    /// <param name="result">The result of the memcached operation.</param>
    /// <param name="isResultEmpty">If set to <c>true</c>, the result is empty.</param>
    public static MemcachedClientValueResult<T> Successful(T result, bool isResultEmpty)
        => new(success: true, result: result, isEmptyResult: isResultEmpty);

    /// <summary>
    /// Creates an instance of <see cref="MemcachedClientValueResult{T}"/> with an unsuccessful result.
    /// </summary>
    /// <param name="errorMessage">The unsuccessful result error message.</param>
    public static MemcachedClientValueResult<T> Unsuccessful(string errorMessage)
        => new(success: false, errorMessage: errorMessage);

    /// <summary>
    /// Creates an instance of <see cref="MemcachedClientValueResult{T}"/> with an unsuccessful result
    /// and a specified default result value.
    /// </summary>
    /// <param name="errorMessage">The unsuccessful result error message.</param>
    /// <param name="defaultResultValue">The default value for <see cref="Result"/> property.</param>
    public static MemcachedClientValueResult<T> Unsuccessful(string errorMessage, T defaultResultValue)
        => new(success: false, result: defaultResultValue, errorMessage: errorMessage);

    /// <summary>
    /// Creates an instance of <see cref="MemcachedClientValueResult{T}"/> that indicates request cancellation.
    /// </summary>
    internal static MemcachedClientValueResult<T> Cancelled(string operationName)
        => new(success: false, isRequestCancelled: true, errorMessage: operationName);

    /// <summary>
    /// Creates an instance of <see cref="MemcachedClientValueResult{T}"/> that indicates request cancellation.
    /// </summary>
    /// <param name="defaultResultValue">The default value for <see cref="Result"/> property.</param>
    internal static MemcachedClientValueResult<T> Cancelled(string operationName, T defaultResultValue)
        => new(
            success: false,
            isRequestCancelled: true,
            result: defaultResultValue,
            errorMessage: operationName);
}