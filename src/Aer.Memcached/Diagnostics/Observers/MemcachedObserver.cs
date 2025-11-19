using System.Collections.Concurrent;
using Aer.ConsistentHash.Abstractions;
using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Models;
using Microsoft.Extensions.DiagnosticAdapter;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace Aer.Memcached.Diagnostics.Observers;

/// <summary>
/// Diagnostic observer for Memcached operations.
/// </summary>
internal sealed class MemcachedObserver
{
    private const string DbSystemValue = "memcached";
    private const string SpanNamePrefix = "memcached ";
    private const string CommandFailedMessage = "Command failed";
    
    // Attribute names
    private const string DbSystemAttribute = "db.system";
    private const string DbOperationNameAttribute = "db.operation.name";
    private const string ServerAddressAttribute = "server.address";
    private const string MemcachedNodeIsReplicatedAttribute = "memcached.node.is_replicated";
    private const string MemcachedReplicaCountAttribute = "memcached.replica.count";
    
    private readonly Tracer _tracer;
    private readonly ILogger<MemcachedObserver> _logger;
    private readonly ConcurrentDictionary<Guid, TelemetrySpan> _commandOpenSpans = new();

    public MemcachedObserver(
        Tracer tracer,
        ILogger<MemcachedObserver> logger)
    {
        _tracer = tracer;
        _logger = logger;
    }

    [DiagnosticName("Aer.Diagnostics.Memcached.CommandExecuteBefore")]
    public void OnCommandExecuteBefore(MemcachedCommandBase command, INode node, bool isReplicated, int replicaCount)
    {
        try
        {
            // Skip if span already exists for this command (e.g., retry scenarios)
            if (_commandOpenSpans.ContainsKey(command.OperationId))
            {
                return;
            }
            
            var commandName = command.OpCode.ToString();
            var nodeKey = node.GetKey();

            // Create span following OpenTelemetry semantic conventions
            var span = _tracer.StartActiveSpan(
                    $"{SpanNamePrefix}{commandName}",
                    SpanKind.Client,
                    Tracer.CurrentSpan)
                // Database semantic conventions
                .SetAttribute(DbSystemAttribute, DbSystemValue)
                .SetAttribute(DbOperationNameAttribute, commandName)
                // Network semantic conventions
                .SetAttribute(ServerAddressAttribute, nodeKey);
            
            if (isReplicated)
            {
                span.SetAttribute(MemcachedNodeIsReplicatedAttribute, true);
                span.SetAttribute(MemcachedReplicaCountAttribute, replicaCount);
            }

            _commandOpenSpans.TryAdd(command.OperationId, span);
        }
        catch (ObjectDisposedException)
        {
            // Activity source might be disposed if request is already completed
            // This can happen with fire-and-forget operations
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception happened during {ObserverName} {MethodName} method call", 
                nameof(MemcachedObserver), nameof(OnCommandExecuteBefore));
        }
    }

    [DiagnosticName("Aer.Diagnostics.Memcached.CommandExecuteAfter")]
    public void OnCommandExecuteAfter(MemcachedCommandBase command, CommandExecutionResult result)
    {
        if (!_commandOpenSpans.TryRemove(command.OperationId, out var span))
        {
            return;
        }

        // Set result status
        if (!result.Success)
        {
            span.SetStatus(Status.Error.WithDescription(result.ErrorMessage ?? CommandFailedMessage));
        }

        span.End();
    }

    [DiagnosticName("Aer.Diagnostics.Memcached.CommandExecuteError")]
    public void OnCommandExecuteError(MemcachedCommandBase command, Exception exception)
    {
        if (!_commandOpenSpans.TryRemove(command.OperationId, out var span))
        {
            return;
        }

        span.End(exception);
    }
}


