using Prometheus.Client;

namespace Aer.Memcached.Diagnostics;

public class MemcachedMetrics
{
    private readonly IMetricFamily<IHistogram> _commandDurationSeconds;
    private readonly IMetricFamily<ICounter> _commandsTotal;

    private const string CommandNameLabel = "command_name";
    private const string IsSuccessfulLabel = "is_successful";
    
    
    public MemcachedMetrics(MetricFactory metricFactory)
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

        _commandsTotal = metricFactory.CreateCounter(
            "memcached_commands_total", 
            "",
            labelNames: new[] { CommandNameLabel, IsSuccessfulLabel });
    }

    /// <summary>
    /// Observes duration of a command
    /// </summary>
    /// <param name="commandName">Name of a command</param>
    /// <param name="duration">Duration of a command</param>
    public void CommandDurationSecondsObserve(string commandName, double duration)
    {
        _commandDurationSeconds
            .WithLabels(commandName)
            .Observe(duration);
    }

    /// <summary>
    /// Observes total number of commands
    /// </summary>
    /// <param name="commandName">Name of a command</param>
    /// <param name="isSuccessful">Command executed successfully or not. 0 and 1 as values</param>
    public void CommandsTotalObserve(string commandName, string isSuccessful)
    {
        _commandsTotal
            .WithLabels(commandName, isSuccessful)
            .Inc();
    }
}