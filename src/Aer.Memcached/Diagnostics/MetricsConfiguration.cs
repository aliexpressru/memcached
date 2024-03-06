using System.Collections.Concurrent;
using OpenTelemetry.Metrics;

namespace Aer.Memcached.Diagnostics;

internal static class MetricsConfiguration
{
	/// <summary>
	/// Stores metric key and bucket configuration for a histogram metrics with explicit bucket boundaries.<br/>
	/// </summary>
	public static ConcurrentDictionary<string, ExplicitBucketHistogramConfiguration> MetricViews { get; } = new();
}
