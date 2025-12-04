using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Diagnostics;
using Microsoft.Extensions.DiagnosticAdapter;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Diagnostics.Listeners;

internal class MetricsMemcachedDiagnosticListener
{
    private readonly MemcachedMetricsProvider _metricsProvider;
    private readonly MemcachedConfiguration _config;

    public MetricsMemcachedDiagnosticListener(
        MemcachedMetricsProvider metricsProvider,
        IOptions<MemcachedConfiguration> config)
    {
        _metricsProvider = metricsProvider;
        _config = config.Value;
    }

    [DiagnosticName(MemcachedDiagnosticSource.CommandDurationDiagnosticName)]
    public void ObserveCommandDuration(string commandName, double duration)
    {
        if (_config.Diagnostics.DisableDiagnostics)
        {
            return;
        }

        _metricsProvider.ObserveCommandDurationSeconds(commandName, duration);
    }

    [DiagnosticName(MemcachedDiagnosticSource.CommandsTotalDiagnosticName)]
    public void ObserveCommandsTotal(string commandName, string isSuccessful)
    {
        if (_config.Diagnostics.DisableDiagnostics)
        {
            return;
        }

        _metricsProvider.ObserveExecutedCommand(commandName, isSuccessful);
    }

    [DiagnosticName(MemcachedDiagnosticSource.SocketPoolUsedSocketCountDiagnosticName)]
    public void ObserveSocketPoolUsedSocketsCount(string enpointAddress, int usedSocketCount)
    {
        if (_config.Diagnostics.DisableDiagnostics)
        {
            return;
        }

        _metricsProvider.ObserveSocketPoolUsedSocketsCount(enpointAddress, usedSocketCount);
    }

    [DiagnosticName(MemcachedDiagnosticSource.SocketPoolExhaustedDiagnosticName)]
    public void ObserveSocketPoolExhausted(string endpointAddress, int maxPoolSize, int usedSocketCount)
    {
        if (_config.Diagnostics.DisableDiagnostics)
        {
            return;
        }

        _metricsProvider.ObserveSocketPoolExhausted(endpointAddress);
    }

    [DiagnosticName(MemcachedDiagnosticSource.SocketPoolRecoveredDiagnosticName)]
    public void ObserveSocketPoolRecovered(string endpointAddress)
    {
        if (_config.Diagnostics.DisableDiagnostics)
        {
            return;
        }

        _metricsProvider.ObserveSocketPoolRecovered(endpointAddress);
    }
}