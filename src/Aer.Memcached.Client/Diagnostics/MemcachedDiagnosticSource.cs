using System.Diagnostics;

namespace Aer.Memcached.Client.Diagnostics;

public class MemcachedDiagnosticSource: DiagnosticListener
{
    private const string SourceName = "Aer.Diagnostics.Memcached";

    public const string CommandDurationDiagnosticName = SourceName + ".CommandDuration";
    public const string CommandsTotalDiagnosticName = SourceName + ".CommandsTotal";
    
    // Tracing diagnostic events (before/after pattern like Platform.Tracing)
    public const string CommandExecuteBeforeDiagnosticName = SourceName + ".CommandExecuteBefore";
    public const string CommandExecuteAfterDiagnosticName = SourceName + ".CommandExecuteAfter";
    public const string CommandExecuteErrorDiagnosticName = SourceName + ".CommandExecuteError";

    public const string SocketPoolSocketCreatedDiagnosticName = SourceName + ".SocketPool.SocketCreated";
    public const string SocketPoolSocketDestroyedDiagnosticName = SourceName + ".SocketPool.SocketDestroyed";

    public const string SocketPoolUsedSocketCountDiagnosticName = SourceName + ".SocketPool.UsedSocketCount";

    public static MemcachedDiagnosticSource Instance { get; } = new();
    
    public MemcachedDiagnosticSource() : base(SourceName)
    {
    }
}