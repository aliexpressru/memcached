using OpenTelemetry.Trace;

namespace Aer.Memcached.Client.Diagnostics;

/// <summary>
/// Disposable scope that manages the lifecycle of a tracing span.
/// </summary>
internal sealed class TracingScope : IDisposable
{
    private readonly TelemetrySpan _span;
    private bool _disposed;

    internal TracingScope(TelemetrySpan span)
    {
        _span = span ?? throw new ArgumentNullException(nameof(span));
    }

    /// <summary>
    /// Sets the result status on the span.
    /// </summary>
    /// <param name="success">Whether the operation was successful.</param>
    /// <param name="errorMessage">Optional error message if operation failed.</param>
    public void SetResult(bool success, string errorMessage = null)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _span.SetStatus(success ? Status.Ok : Status.Error.WithDescription(errorMessage ?? "Operation failed"));
        }
        catch
        {
            // Tracing should never break the application
            // Silently ignore any exceptions from tracing operations
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

        try
        {
            _span.RecordException(exception);
            _span.SetStatus(Status.Error.WithDescription(exception.Message));
        }
        catch
        {
            // Tracing should never break the application
            // Silently ignore any exceptions from tracing operations
        }
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

        try
        {
            _span.End();
        }
        catch
        {
            // Tracing should never break the application
            // Silently ignore any exceptions from tracing operations
        }
        finally
        {
            _disposed = true;
        }
    }
}