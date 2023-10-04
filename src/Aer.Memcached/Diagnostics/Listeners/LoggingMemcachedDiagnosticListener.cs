using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Diagnostics;
using Microsoft.Extensions.DiagnosticAdapter;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Diagnostics.Listeners;

public class LoggingMemcachedDiagnosticListener
{
    private readonly ILogger<LoggingMemcachedDiagnosticListener> _logger;
    private readonly MemcachedConfiguration _config;

    public LoggingMemcachedDiagnosticListener(ILogger<LoggingMemcachedDiagnosticListener> logger, IOptions<MemcachedConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    [DiagnosticName(MemcachedDiagnosticSource.SocketPoolSocketCreatedDiagnosticName)]
    public void ObserveSocketPoolSocketCreated(string enpointAddress)
    {
        if (_config.Diagnostics.DisableSocketPoolDiagnosticsLogging)
        {
            return;
        }

        _logger.Log(
            _config.Diagnostics.SocketPoolDiagnosticsLoggingEventLevel,
            "Created pooled socket for endpoint {EndPoint}",
            enpointAddress);
    }

    [DiagnosticName(MemcachedDiagnosticSource.SocketPoolSocketDestroyedDiagnosticName)]
    public void ObserveSocketPoolSocketDestroyed(string enpointAddress)
    {
        if (_config.Diagnostics.DisableSocketPoolDiagnosticsLogging)
        {
            return;
        }

        _logger.Log(
            _config.Diagnostics.SocketPoolDiagnosticsLoggingEventLevel,
            "Destroyed pooled socket for endpoint {EndPoint}",
            enpointAddress);
    }
}