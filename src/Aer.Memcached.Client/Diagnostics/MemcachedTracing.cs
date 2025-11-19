using System.Diagnostics;
using Aer.ConsistentHash.Abstractions;
using Aer.Memcached.Client.Commands.Base;
using OpenTelemetry.Trace;

namespace Aer.Memcached.Client.Diagnostics;

/// <summary>
/// Helper class for Memcached tracing operations.
/// </summary>
internal static class MemcachedTracing
{
    private const string DbSystemValue = "memcached";
    private const string SpanNamePrefix = "memcached ";
    
    // Attribute names
    private const string DbSystemAttribute = "db.system";
    private const string DbOperationNameAttribute = "db.operation.name";
    private const string ServerAddressAttribute = "server.address";
    private const string MemcachedNodeIsReplicatedAttribute = "memcached.node.is_replicated";
    private const string MemcachedReplicaCountAttribute = "memcached.replica.count";

    /// <summary>
    /// Creates a tracing scope for a memcached command.
    /// Returns null if tracing is not enabled.
    /// </summary>
    public static CommandTracingScope CreateCommandScope(
        Tracer tracer,
        MemcachedCommandBase command,
        INode node,
        bool isReplicated,
        int replicaCount)
    {
        if (tracer == null || Activity.Current == null)
        {
            return null;
        }

        try
        {
            var commandName = command.OpCode.ToString();
            var nodeKey = node.GetKey();

            // Create span following OpenTelemetry semantic conventions
            var span = tracer.StartActiveSpan(
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

            return new CommandTracingScope(span);
        }
        catch (ObjectDisposedException)
        {
            // Activity source might be disposed if request is already completed
            return null;
        }
    }
}

