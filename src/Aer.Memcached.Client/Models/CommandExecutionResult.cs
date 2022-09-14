namespace Aer.Memcached.Client.Models;

public class CommandExecutionResult
{
    public static CommandExecutionResult Unsuccessful { get; } = new()
    {
        Success = false
    };

    public static CommandExecutionResult Successful { get; } = new()
    {
        Success = true
    };
    
    public bool Success { get; init; }
}