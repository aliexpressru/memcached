using System.Diagnostics;

namespace Aer.Memcached.Client.Diagnostics;

public class MemcachedDiagnosticSource: DiagnosticListener
{
    private const string SourceName = "Aer.Diagnostics.Memcached";

    public const string CommandDurationDiagnosticName = SourceName + ".CommandDuration";
    public const string CommandsTotalDiagnosticName = SourceName + ".CommandsTotal";

    public static MemcachedDiagnosticSource Instance { get; } = new();
    
    public MemcachedDiagnosticSource() : base(SourceName)
    {
    }
}