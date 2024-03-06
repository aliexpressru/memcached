using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace Aer.Memcached.Diagnostics.Extensions;

/// <summary>
/// Extension methods to simplify registering of dependency instrumentation.
/// </summary>
internal static class MetricsExtensions
{
    /// <summary>
    /// Histogram is an Instrument which can be used to report arbitrary values that are likely to be statistically
    /// meaningful. It is intended for statistics such as histograms, summaries, and percentile.
    /// </summary>
    /// <param name="name">The instrument name. cannot be null.</param>
    /// <param name="boundaries">
    /// Boundaries of the histogram metric stream (buckets configuration).
    /// See <see cref="ExplicitBucketHistogramConfiguration.Boundaries"/> for more info.
    /// </param>
    /// <param name="unit">Optional instrument unit of measurements.</param>
    /// <param name="description">Optional instrument description.</param>
    /// <remarks>
    /// Example uses for Histogram: the request duration and the size of the response payload.
    /// </remarks>
    public static Histogram<T> CreateHistogram<T>(
        this Meter meter,
        string name,
        double[] boundaries,
        string unit = null,
        string description = null)
        where T : struct => meter.CreateHistogram<T>(name, boundaries, unit, description, tags: null);

    /// <summary>
    /// Histogram is an Instrument which can be used to report arbitrary values that are likely to be statistically
    /// meaningful. It is intended for statistics such as histograms, summaries, and percentile.
    /// </summary>
    /// <param name="name">The instrument name. cannot be null.</param>
    /// <param name="boundaries">
    /// Boundaries of the histogram metric stream (buckets configuration).
    /// See <see cref="ExplicitBucketHistogramConfiguration.Boundaries"/> for more info.
    /// </param>
    /// <param name="unit">Optional instrument unit of measurements.</param>
    /// <param name="description">Optional instrument description.</param>
    /// <param name="tags">tags to attach to the counter.</param>
    /// <remarks>
    /// Example uses for Histogram: the request duration and the size of the response payload.
    /// </remarks>
    public static Histogram<T> CreateHistogram<T>(
        this Meter meter,
        string name,
        double[] boundaries,
        string unit,
        string description,
        IEnumerable<KeyValuePair<string, object>> tags)
        where T : struct
    {
        MetricsConfiguration.MetricViews.TryAdd(
            name,
            new ExplicitBucketHistogramConfiguration {Boundaries = boundaries});

        return meter.CreateHistogram<T>(
            name,
            unit,
            description,
            tags);
    }

    public static void AddMetrics(this IServiceCollection services, string meterName)
    {
        services.AddOpenTelemetry().WithMetrics(
            builder =>
            {
                builder.AddMeter(meterName);
                
                if (MetricsConfiguration.MetricViews is {Count: > 0})
                {
                    builder.AddView(instrument => MetricsConfiguration.MetricViews.GetValueOrDefault(instrument.Name));
                }
            });
    }
}

