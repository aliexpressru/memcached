using Aer.Memcached.Client.Models;
using OpenTelemetry.Trace;

namespace Aer.Memcached.Client.Diagnostics;

/// <summary>
/// Disposable scope that manages the lifecycle of a tracing span for a memcached command.
/// </summary>
internal sealed class CommandTracingScope : IDisposable
{
    private readonly TelemetrySpan _span;
    private bool _disposed;

    internal CommandTracingScope(TelemetrySpan span)
    {
        _span = span ?? throw new ArgumentNullException(nameof(span));
    }

    /// <summary>
    /// Sets the result status on the span based on command execution result.
    /// </summary>
    public void SetResult(CommandExecutionResult result)
    {
        if (_disposed)
        {
            return;
        }

        if (!result.Success)
        {
            _span.SetStatus(Status.Error.WithDescription(result.ErrorMessage ?? "Command failed"));
        }
    }

    /// <summary>
    /// Sets an error status on the span based on an exception.
    /// </summary>
    public void SetError(Exception exception)
    {
        if (_disposed)
        {
            return;
        }

        _span.RecordException(exception);
        _span.SetStatus(Status.Error.WithDescription(exception.Message));
    }

    /// <summary>
    /// Ends the span and disposes the scope.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _span.End();
        _disposed = true;
    }
}

