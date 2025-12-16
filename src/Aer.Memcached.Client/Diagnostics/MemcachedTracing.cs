using System.Diagnostics;
using Aer.ConsistentHash.Abstractions;
using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Models;
using Microsoft.Extensions.Logging;
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
    private const string SocketPoolMaxSizeAttribute = "memcached.socket_pool.max_size";
    private const string SocketPoolUsedCountAttribute = "memcached.socket_pool.used_count";
    private const string CacheSyncServerAttribute = "memcached.cache_sync.server";
    private const string CacheSyncKeysCountAttribute = "memcached.cache_sync.keys_count";

    /// <summary>
    /// Determines whether tracing should be enabled based on global EnableTracing configuration,
    /// current activity context, and tracing options.
    /// </summary>
    private static bool ShouldEnableTracing(TracingOptions tracingOptions, bool enableTracing)
    {
        // Global EnableTracing = false always disables tracing, regardless of Tracer availability
        if (!enableTracing)
        {
            return false;
        }
        
        // No parent activity context - nothing to trace to
        if (Activity.Current == null)
        {
            return false;
        }

        // Manual per-operation disable
        if (tracingOptions?.ManualDisableTracing == true)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Creates a tracing scope for a memcached command.
    /// Returns null if tracing is not enabled.
    /// </summary>
    public static TracingScope CreateCommandScope(
        Tracer tracer,
        MemcachedCommandBase command,
        INode node,
        bool isReplicated,
        int replicaCount,
        bool enableTracing,
        ILogger logger = null,
        TracingOptions tracingOptions = null)
    {
        if (!ShouldEnableTracing(tracingOptions, enableTracing))
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

            return new TracingScope(span);
        }
        catch (Exception ex)
        {
            // Tracing should never break the application
            // Common cases:
            // - ObjectDisposedException: Activity source disposed when request completed
            // - NullReferenceException: HTTP context disposed during fire-and-forget operations
            logger?.LogWarning(ex, 
                "Failed to create tracing scope for command {OpCode} on node {NodeKey}. Tracing will be disabled for this operation.",
                command?.OpCode, node?.GetKey());
            return null;
        }
    }

    /// <summary>
    /// Creates a tracing scope for socket pool operations.
    /// Returns null if tracing is not enabled.
    /// </summary>
    public static TracingScope CreateSocketOperationScope(
        Tracer tracer,
        string operationName,
        string serverAddress,
        bool enableTracing,
        int? maxPoolSize = null,
        int? usedCount = null,
        ILogger logger = null,
        TracingOptions tracingOptions = null)
    {
        if (!ShouldEnableTracing(tracingOptions, enableTracing))
        {
            return null;
        }

        try
        {
            var span = tracer.StartActiveSpan(
                    $"{SpanNamePrefix}{operationName}",
                    SpanKind.Client,
                    Tracer.CurrentSpan)
                .SetAttribute(DbSystemAttribute, DbSystemValue)
                .SetAttribute(DbOperationNameAttribute, operationName)
                .SetAttribute(ServerAddressAttribute, serverAddress);

            if (maxPoolSize.HasValue)
            {
                span.SetAttribute(SocketPoolMaxSizeAttribute, maxPoolSize.Value);
            }

            if (usedCount.HasValue)
            {
                span.SetAttribute(SocketPoolUsedCountAttribute, usedCount.Value);
            }

            return new TracingScope(span);
        }
        catch (Exception ex)
        {
            // Tracing should never break the application
            logger?.LogWarning(ex,
                "Failed to create tracing scope for socket operation {OperationName} on {ServerAddress}. Tracing will be disabled for this operation.",
                operationName, serverAddress);
            return null;
        }
    }

    /// <summary>
    /// Creates a tracing scope for cache synchronization operations.
    /// Returns null if tracing is not enabled.
    /// </summary>
    public static TracingScope CreateCacheSyncScope(
        Tracer tracer,
        string operationName,
        string syncServer,
        bool enableTracing,
        int? keysCount = null,
        ILogger logger = null,
        TracingOptions tracingOptions = null)
    {
        if (!ShouldEnableTracing(tracingOptions, enableTracing))
        {
            return null;
        }

        try
        {
            var span = tracer.StartActiveSpan(
                    $"{SpanNamePrefix}{operationName}",
                    SpanKind.Client,
                    Tracer.CurrentSpan)
                .SetAttribute(DbSystemAttribute, DbSystemValue)
                .SetAttribute(DbOperationNameAttribute, operationName)
                .SetAttribute(CacheSyncServerAttribute, syncServer);

            if (keysCount.HasValue)
            {
                span.SetAttribute(CacheSyncKeysCountAttribute, keysCount.Value);
            }

            return new TracingScope(span);
        }
        catch (Exception ex)
        {
            // Tracing should never break the application
            logger?.LogWarning(ex,
                "Failed to create tracing scope for cache sync operation {OperationName} on {SyncServer}. Tracing will be disabled for this operation.",
                operationName, syncServer);
            return null;
        }
    }
}

