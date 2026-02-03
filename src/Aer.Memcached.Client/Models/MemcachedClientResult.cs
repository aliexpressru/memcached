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
    public bool SyncSuccess { get; private init; }

    /// <summary>
    /// If set to <c>true</c>, then operation was disabled.
    /// </summary>
    public bool OperationDisabled { get; }

    /// <summary>
    /// Gets an instance of <see cref="MemcachedClientResult"/> with a successful result.
    /// </summary>
    public static MemcachedClientResult Successful { get; } = new(success: true);

    internal MemcachedClientResult(
        bool success,
        string errorMessage = null,
        bool isRequestCancelled = false,
        bool operationDisabled = false)
    {
        Success = success;
        ErrorMessage = errorMessage;
        RequestCancelled = isRequestCancelled;
        OperationDisabled = operationDisabled;
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
    internal static MemcachedClientResult Disabled() => new(success: true, operationDisabled: true);

    /// <summary>
    /// Creates a deep copy of the current <see cref="MemcachedClientResult"/> instance
    /// with a new <see cref="SyncSuccess"/> value.
    /// </summary>
    /// <param name="syncSuccess">The new SyncSuccess value for the copy.</param>
    /// <returns>A new instance with the specified SyncSuccess value.</returns>
    public MemcachedClientResult CopyWithSyncSuccess(bool syncSuccess)
    {
        return new MemcachedClientResult(
            success: Success,
            errorMessage: ErrorMessage,
            isRequestCancelled: RequestCancelled,
            operationDisabled: OperationDisabled)
        {
            SyncSuccess = syncSuccess
        };
    }
}
