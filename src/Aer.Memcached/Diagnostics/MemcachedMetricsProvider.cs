using System.Diagnostics.Metrics;
using Aer.Memcached.Client.Config;
using OpenTelemetry.Metrics;
using Prometheus.Client;
using Aer.Memcached.Diagnostics.Extensions;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Diagnostics;

internal class MemcachedMetricsProvider
{
    public static readonly string MeterName = "Aer.Mont.Service.Kv.Metrics";

    // open telemetry metrics
    private readonly Histogram<double> _commandDurationSecondsOtel;
    private readonly Histogram<int> _socketPoolUsedSocketsCountsOtel;
    private readonly Counter<int> _commandsTotalOtel;

    // Prometheus metrics
    private readonly IMetricFamily<IHistogram> _commandDurationSeconds;
    private readonly IMetricFamily<IHistogram> _socketPoolUsedSocketsCounts;
    private readonly IMetricFamily<ICounter> _commandsTotal;

    private const string CommandNameLabel = "command_name";
    private const string IsSuccessfulLabel = "is_successful";
    private const string SocketPoolEndpointAddressLabel = "socket_pool_endpoint_address";

    public MemcachedMetricsProvider(
        IMetricFactory metricFactory,
        IMeterFactory meterFactory,
        IOptions<MemcachedConfiguration> configuration)
    {
        if (!Enum.TryParse(configuration.Value.MetricsProviderName, out MetricsProvider metricsProvider))
        {
            // if metrics provider is not set or set incorrectly - use default behavior : Prometheus metrics
            metricsProvider = MetricsProvider.Prometheus;
        }

        // Prometheus metrics
        _commandDurationSeconds = metricFactory.CreateHistogram(
            "memcached_command_duration_seconds",
            "",
            new[] {0.0005, 0.001, 0.005, 0.007, 0.015, 0.05, 0.2, 0.5, 1},
            labelNames: new[] {CommandNameLabel});

        _socketPoolUsedSocketsCounts = metricFactory.CreateHistogram(
            "memecached_socket_pool_used_sockets",
            "",
            new[]
            {
                0, 10.0, 20.0, 50.0, 100.0, 200.0, 500.0
            },
            labelNames: new[] {SocketPoolEndpointAddressLabel});

        _commandsTotal = metricFactory.CreateCounter(
            "memcached_commands_total",
            "",
            labelNames: new[] {CommandNameLabel, IsSuccessfulLabel});

        // open telemetry metrics

        var meter = meterFactory.Create(MeterName);

        _commandDurationSecondsOtel = meter.CreateHistogram<double>(
            name: "memcached_command_duration_seconds",
            unit: "second",
            description: "Memcached command duration in seconds",
            boundaries: new[] {0.0005, 0.001, 0.005, 0.007, 0.015, 0.05, 0.2, 0.5, 1});

        _socketPoolUsedSocketsCountsOtel = meter.CreateHistogram<int>(
            name: "memecached_socket_pool_used_sockets",
            unit: null,
            description: "Number of used socket pool sockets",
            boundaries: new[]
            {
                0, 10.0, 20.0, 50.0, 100.0, 200.0, 500.0
            });

        _commandsTotalOtel = meter.CreateCounter<int>(
            name: "memcached_commands_total",
            unit: null,
            description: "Number of total executed memcached commands");
    }

    public static void RegisterMeter(MeterProviderBuilder meterProviderBuilder)
    {
        meterProviderBuilder.AddMeter(MeterName);
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
    /// <param name="enpointAddress">The address of an endpoint to obeserve socket pool state for.</param>
    /// <param name="usedSocketCount">The number of currently used sockets for the specified pool.</param>
    public void ObserveSocketPoolUsedSocketsCount(string enpointAddress, int usedSocketCount)
    {
        _socketPoolUsedSocketsCountsOtel?.Record(
            usedSocketCount,
            new KeyValuePair<string, object>(
                SocketPoolEndpointAddressLabel,
                enpointAddress));

        _socketPoolUsedSocketsCounts
            ?.WithLabels(enpointAddress)
            ?.Observe(usedSocketCount);
    }
}