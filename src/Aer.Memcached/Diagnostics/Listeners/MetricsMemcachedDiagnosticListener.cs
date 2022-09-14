using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Diagnostics;
using Microsoft.Extensions.DiagnosticAdapter;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Diagnostics.Listeners;

public class MetricsMemcachedDiagnosticListener
{
    private readonly MemcachedMetrics _metrics;
    private readonly MemcachedConfiguration _config;

    public MetricsMemcachedDiagnosticListener(MemcachedMetrics metrics, IOptions<MemcachedConfiguration> config)
    {
        _metrics = metrics;
        _config = config.Value;
    }

    [DiagnosticName(MemcachedDiagnosticSource.CommandDurationDiagnosticName)]
    public void ObserveCommandDuration(string commandName, double duration)
    {
        if (_config.Diagnostics.DisableDiagnostics)
        {
            return;
        }
        
        _metrics.CommandDurationSecondsObserve(commandName, duration);
    }
    
    [DiagnosticName(MemcachedDiagnosticSource.CommandsTotalDiagnosticName)]
    public void ObserveCommandsTotal(string commandName, string isSuccessful)
    {
        if (_config.Diagnostics.DisableDiagnostics)
        {
            return;
        }
        
        _metrics.CommandsTotalObserve(commandName, isSuccessful);
    }
}