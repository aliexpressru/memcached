using System.Diagnostics.Metrics;
using Aer.Memcached.Client.Config;
using Aer.Memcached.Diagnostics.Configuration;
using Prometheus.Client;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Diagnostics;

internal class MemcachedMetricsProvider
{
    public static readonly string MeterName = "Aer.Memcached.Metrics";

    // open telemetry metrics
    private readonly Histogram<double> _commandDurationSecondsOtel;
    private readonly Histogram<int> _socketPoolUsedSocketsCountsOtel;
    private readonly Counter<int> _commandsTotalOtel;

    // Prometheus metrics
    private readonly IMetricFamily<IHistogram> _commandDurationSeconds;
    private readonly IMetricFamily<IHistogram> _socketPoolUsedSocketsCounts;
    private readonly IMetricFamily<ICounter> _commandsTotal;

    private const string CommandDurationSecondsMetricName = "memcached_command_duration_seconds";
    private const string SocketPoolUsedSocketsCountsMetricName = "memecached_socket_pool_used_sockets";
    private const string CommandsTotalOtelMetricName = "memcached_commands_total";

    public static readonly Dictionary<string, double[]> MetricsBuckets = new()
    {
        [CommandDurationSecondsMetricName] = new[] {0.0005, 0.001, 0.005, 0.007, 0.015, 0.05, 0.2, 0.5, 1},
        [SocketPoolUsedSocketsCountsMetricName] = new[] {0, 10.0, 20.0, 50.0, 100.0, 200.0, 500.0}
    };
    
    private const string CommandNameLabel = "command_name";
    private const string IsSuccessfulLabel = "is_successful";
    private const string SocketPoolEndpointAddressLabel = "socket_pool_endpoint_address";

    public MemcachedMetricsProvider(
        IMetricFactory metricFactory,
        IMeterFactory meterFactory,
        IOptions<MemcachedConfiguration> configuration)
    {
        if (!Enum.TryParse(configuration.Value.MetricsProviderName, out MetricsProviderType metricsProvider))
        {
            // if metrics provider is not set or set incorrectly - use default behavior : Prometheus metrics
            metricsProvider = MetricsProviderType.Prometheus;
        }

        if (metricsProvider == MetricsProviderType.OpenTelemetry)
        {
            // use open telemetry metrics

            ArgumentNullException.ThrowIfNull(meterFactory);

            var meter = meterFactory.Create(MeterName);

            _commandDurationSecondsOtel = meter.CreateHistogram<double>(
                name: CommandDurationSecondsMetricName,
                unit: "second",
                description: "Memcached command duration in seconds");

            _socketPoolUsedSocketsCountsOtel = meter.CreateHistogram<int>(
                name: SocketPoolUsedSocketsCountsMetricName,
                unit: null,
                description: "Number of used socket pool sockets");

            _commandsTotalOtel = meter.CreateCounter<int>(
                name: CommandsTotalOtelMetricName,
                unit: null,
                description: "Number of total executed memcached commands");

            return;
        }

        // use Prometheus metrics
        
        ArgumentNullException.ThrowIfNull(metricFactory);
        
        _commandDurationSeconds = metricFactory.CreateHistogram(
            CommandDurationSecondsMetricName,
            "",
            MetricsBuckets[CommandDurationSecondsMetricName],
            labelNames: new[] {CommandNameLabel});

        _socketPoolUsedSocketsCounts = metricFactory.CreateHistogram(
            SocketPoolUsedSocketsCountsMetricName,
            "",
            MetricsBuckets[SocketPoolUsedSocketsCountsMetricName],
            labelNames: new[] {SocketPoolEndpointAddressLabel});

        _commandsTotal = metricFactory.CreateCounter(
            CommandsTotalOtelMetricName,
            "",
            labelNames: new[] {CommandNameLabel, IsSuccessfulLabel});
    }

    /// <summary>
    /// Observes the duration of a command.
    /// </summary>
    /// <param name="commandName">Name of a command.</param>
    /// <param name="durationSeconds">Duration of a command in seconds.</param>
    public void ObserveCommandDurationSeconds(string commandName, double durationSeconds)
    {
        _commandDurationSecondsOtel?.Record(
            durationSeconds,
            new KeyValuePair<string, object>(CommandNameLabel, commandName));

        _commandDurationSeconds?.WithLabels(commandName)?.Observe(durationSeconds);
    }

    /// <summary>
    /// Observes an executed command.
    /// </summary>
    /// <param name="commandName">Name of a command.</param>
    /// <param name="isSuccessful">Command executed successfully or not. 0 and 1 as values.</param>
    public void ObserveExecutedCommand(string commandName, string isSuccessful)
    {
        _commandsTotalOtel?.Add(
            1,
            new KeyValuePair<string, object>[]
            {
                new(CommandNameLabel, commandName), new(IsSuccessfulLabel, isSuccessful)
            });

        _commandsTotal
            ?.WithLabels(commandName, isSuccessful)
            ?.Inc();
    }

    /// <summary>
    /// Observes specified endpoint socket pool used sockets count.
    /// </summary>
    /// <param name="endpointAddress">The address of an endpoint to obeserve socket pool state for.</param>
    /// <param name="usedSocketCount">The number of currently used sockets for the specified pool.</param>
    public void ObserveSocketPoolUsedSocketsCount(string endpointAddress, int usedSocketCount)
    {
        _socketPoolUsedSocketsCountsOtel?.Record(
            usedSocketCount,
            new KeyValuePair<string, object>(
                SocketPoolEndpointAddressLabel,
                endpointAddress));

        _socketPoolUsedSocketsCounts
            ?.WithLabels(endpointAddress)
            ?.Observe(usedSocketCount);
    }
}