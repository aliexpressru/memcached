using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Aer.Memcached.Client.Models;
using Microsoft.Extensions.Logging;

namespace Aer.Memcached.Client.Extensions;

/// <summary>
/// Contains extension methods for value and generic client results. 
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public static class MemcachedClientResultExtensions
{
    private const string DefaultErrorMessage =
        "UNKNOWN - see memcached command execution logs : search for substring 'Error occured during command'";

    internal static MemcachedClientResult WithSyncSuccess(this MemcachedClientResult result, bool syncSuccess)
    {
        result.SyncSuccess = syncSuccess;
        return result;
    }

    /// <summary>
    /// Checks client method result and logs error if client method call returned non-successful result. 
    /// </summary>
    /// <param name="target">The client result to check.</param>
    /// <param name="logger">The logger to log error to.</param>
    /// <param name="operationName">
    /// The operation name that called client method. Do not assign - assigned automatically.
    /// </param>
    /// <param name="customErrorMessage">
    /// An optional custom error message to write out instead of a default one.
    /// Expected to not have any structured logging parameter placeholders.
    /// </param>
    /// <param name="logLevel">The logger log level. Default value is <see cref="LogLevel.Error"/>.</param>
    public static void LogErrorIfAny(
        this MemcachedClientResult target,
        ILogger logger,
        [CallerMemberName] string operationName = null,
        string customErrorMessage = null,
        LogLevel logLevel = LogLevel.Error)
    {
        if (target.Success)
        {
            return;
        }

        if (!string.IsNullOrEmpty(customErrorMessage))
        {
            logger.Log(
                logLevel,
                "{ErrorMessage}. Error details : {ErrorDetails}",
                customErrorMessage,
                target.ErrorMessage ?? DefaultErrorMessage);
        }
        else
        {
            logger.Log(
                logLevel,
                "Error happened during memcached {Operation} operation. Error details : {ErrorDetails}",
                operationName,
                target.ErrorMessage ?? DefaultErrorMessage);
        }
    }

    /// <summary>
    /// Checks client method result and logs error if client method call returned non-successful result. 
    /// </summary>
    /// <param name="target">The client result to check.</param>
    /// <param name="logger">The logger to log error to.</param>
    /// <param name="cacheKeysCount">The optional number of keys requested.</param>
    /// <param name="operationName">
    /// The operation name that called client method. Do not assign - assigned automatically.
    /// </param>
    /// <param name="customErrorMessage">
    /// An optional custom error message to write out instead of a default one.
    /// Expected to not have any structured logging parameter placeholders.
    /// When specified <paramref name="cacheKeysCount"/> parameter is ignored.
    /// </param>
    /// <param name="logLevel">The logger log level. Default value is <see cref="LogLevel.Error"/>.</param>
    public static void LogErrorIfAny<T>(
        this MemcachedClientValueResult<T> target,
        ILogger logger,
        int? cacheKeysCount,
        [CallerMemberName] string operationName = null,
        string customErrorMessage = null,
        LogLevel logLevel = LogLevel.Error)
    {
        if (target.Success)
        {
            return;
        }

        switch (cacheKeysCount, customErrorMessage)
        {
            case (_, {Length: > 0} specificErrorMessage):
                logger.Log(
                    logLevel,
                    "{ErrorMessage}. Error details : {ErrorDetails}",
                    specificErrorMessage,
                    target.ErrorMessage ?? DefaultErrorMessage);
                break;

            case ({ } keysCount, null):
                logger.Log(
                    logLevel,
                    "Error happened during memcached {Operation} operation with cache keys count : {CacheKeysCount}. Error details : {ErrorDetails}",
                    operationName,
                    keysCount,
                    target.ErrorMessage ?? DefaultErrorMessage);
                break;

            case (null, null):
                logger.Log(
                    logLevel,
                    "Error happened during memcached {Operation} operation. Error details : {ErrorDetails}",
                    operationName,
                    target.ErrorMessage ?? DefaultErrorMessage);
                break;
        }
    }
}
