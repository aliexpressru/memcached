using OpenTelemetry.Trace;

namespace Aer.Memcached.Diagnostics;

/// <summary>
/// Extension methods for TelemetrySpan to simplify error handling.
/// </summary>
internal static class SpanExtensions
{
    /// <summary>
    /// Ends the span and records exception if provided.
    /// </summary>
    /// <param name="span">The span to end.</param>
    /// <param name="exception">Optional exception to record.</param>
    public static void End(this TelemetrySpan span, Exception exception)
    {
        if (span == null)
        {
            return;
        }

        if (exception != null)
        {
            span.SetStatus(Status.Error);
            span.RecordException(exception);
        }

        span.End();
    }
}

