using System.Diagnostics;
using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Diagnostics;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Commands.Helpers;

public class DiagnosticTimer
{
    private Stopwatch _stopwatch;
    private MemcachedCommandBase _command;
    
    public static DiagnosticTimer StartNew(MemcachedCommandBase command)
    {
        var diagnosticTimer = new DiagnosticTimer();
        diagnosticTimer.Start(command);

        return diagnosticTimer;
    }
    
    public void StopAndWriteDiagnostics(CommandExecutionResult executionResult)
    {
        if (_stopwatch == null)
        {
            return;
        }
        
        _stopwatch.Stop();

        var commandName = _command.OpCode.ToString();

        if (!MemcachedDiagnosticSource.Instance.IsEnabled())
        {
            return;
        }
        
        MemcachedDiagnosticSource.Instance.Write(MemcachedDiagnosticSource.CommandDurationDiagnosticName, new
        {
            commandName = commandName,
            duration = _stopwatch.Elapsed.TotalSeconds
        });
            
        MemcachedDiagnosticSource.Instance.Write(MemcachedDiagnosticSource.CommandsTotalDiagnosticName, new
        {
            commandName = commandName,
            isSuccessful = executionResult.Success ? "1" : "0"
        });
    }

    private void Start(MemcachedCommandBase command)
    {
        _stopwatch = Stopwatch.StartNew();
        _command = command;
    }
}