namespace Aer.Memcached.Client.Models;

/// <summary>
/// Options for controlling tracing behavior of memcached operations.
/// </summary>
public class TracingOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether tracing should be manually disabled for this operation.
    /// When set to true, no tracing spans will be created even if tracing is enabled globally.
    /// Default is false.
    /// </summary>
    public bool ManualDisableTracing { get; set; }
    
    /// <summary>
    /// Gets a singleton instance with tracing disabled.
    /// </summary>
    public static TracingOptions Disabled { get; } = new() { ManualDisableTracing = true };
    
    /// <summary>
    /// Gets a singleton instance with tracing enabled (default behavior).
    /// </summary>
    public static TracingOptions Enabled { get; } = new() { ManualDisableTracing = false };
}

