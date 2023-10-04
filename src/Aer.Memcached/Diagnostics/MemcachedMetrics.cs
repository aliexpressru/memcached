using Prometheus.Client;

namespace Aer.Memcached.Diagnostics;

public class MemcachedMetrics
{
    private readonly IMetricFamily<IHistogram> _commandDurationSeconds;
    private readonly IMetricFamily<IHistogram> _socketPoolUsedSocketsCounts;
    private readonly IMetricFamily<ICounter> _commandsTotal;

    private const string CommandNameLabel = "command_name";
    private const string IsSuccessfulLabel = "is_successful";

    private const string SocketPoolEndpointAddressLabel = "socket_pool_endpoint_address";
    
    public MemcachedMetrics(IMetricFactory metricFactory)
    {
        if (metricFactory == null)
        {
            throw new ArgumentNullException(nameof(metricFactory));
        }

        _commandDurationSeconds = metricFactory.CreateHistogram(
            "memcached_command_duration_seconds",
            "",
            new[] { 0.0005, 0.001, 0.005, 0.007, 0.015, 0.05, 0.2, 0.5, 1 },
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
            labelNames: new[] { CommandNameLabel, IsSuccessfulLabel });
    }

    /// <summary>
    /// Observes the duration of a command.
    /// </summary>
    /// <param name="commandName">Name of a command.</param>
    /// <param name="durationSeconds">Duration of a command in seconds.</param>
    public void ObserveCommandDurationSeconds(string commandName, double durationSeconds)
    {
        _commandDurationSeconds
            .WithLabels(commandName)
            .Observe(durationSeconds);
    }

    /// <summary>
    /// Observes an executed command.
    /// </summary>
    /// <param name="commandName">Name of a command.</param>
    /// <param name="isSuccessful">Command executed successfully or not. 0 and 1 as values.</param>
    public void ObserveExecutedCommand(string commandName, string isSuccessful)
    {
        _commandsTotal
            .WithLabels(commandName, isSuccessful)
            .Inc();
    }
    
    /// <summary>
    /// Observes specified endpoint socket pool used sockets count.
    /// </summary>
    /// <param name="enpointAddress">The address of an endpoint to obeserve socket pool state for.</param>
    /// <param name="usedSocketCount">The number of currently used sockets for the specified pool.</param>
    public void ObserveSocketPoolUsedSocketsCount(string enpointAddress, int usedSocketCount)
    {
        _socketPoolUsedSocketsCounts
            .WithLabels(enpointAddress)
            .Observe(usedSocketCount);
    }
}