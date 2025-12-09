using System.Diagnostics.CodeAnalysis;

namespace Aer.Memcached.Client.Models;

/// <summary>
/// Represents a memcached result without a value.
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public class MemcachedClientResult
{
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
    /// If set to <c>true</c>, then no errors occured on cached sync side.
    /// It is set as <c>false</c> when cache sync is not enabled.
    /// </summary>
    public bool SyncSuccess { get; set; }

    /// <summary>
    /// If set to <c>true</c>, then operation was ignored.
    /// </summary>
    public bool OperationIgnored { get; }

    /// <summary>
    /// Gets an instance of <see cref="MemcachedClientResult"/> with a successful result.
    /// </summary>
    public static MemcachedClientResult Successful { get; } = new(success: true);

    internal MemcachedClientResult(
        bool success,
        string errorMessage = null,
        bool isRequestCancelled = false,
        bool operationIgnored = false)
    {
        Success = success;
        ErrorMessage = errorMessage;
        RequestCancelled = isRequestCancelled;
        OperationIgnored = operationIgnored;
    }

    /// <summary>
    /// Creates an instance of <see cref="MemcachedClientResult"/> with an unsuccessful result.
    /// </summary>
    /// <param name="errorMessage">The unsuccessful result error message.</param>
    public static MemcachedClientResult Unsuccessful(string errorMessage)
        => new(success: false, errorMessage);

    /// <summary>
    /// Gets an instance of <see cref="MemcachedClientResult"/> that indicates request cancellation.
    /// </summary>
    internal static MemcachedClientResult Cancelled(string operationName) => new(success: false, isRequestCancelled: true, errorMessage: operationName);

    /// <summary>
    /// Gets an instance of <see cref="MemcachedClientResult"/> that indicates ignored operation.
    /// </summary>
    internal static MemcachedClientResult Ignored() => new(success: true, operationIgnored: true);
}