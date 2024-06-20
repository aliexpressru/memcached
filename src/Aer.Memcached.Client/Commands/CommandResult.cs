using Aer.Memcached.Client.Commands.Infrastructure;

namespace Aer.Memcached.Client.Commands;

public class CommandResult
{
    public static readonly CommandResult DeadSocket = 
        Fail("Failed to read from the socket. Socket seems to be dead");
    
    /// <summary>
    /// A value indicating whether a command was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// A message indicating success, warning or failure reason for a command.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// An exception that caused a failure.
    /// </summary>
    public Exception Exception { get; set; }

    /// <summary>
    /// The StatusCode returned from the server.
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// A result that influenced the current result.
    /// </summary>
    public CommandResult InnerResult { get; set; }
    
    public ulong Cas { get; set; }

    public static CommandResult Fail(string message, Exception exception = null)
        =>
            new()
            {
                Success = false,
                Message = message,
                Exception = exception,
                StatusCode = BinaryResponseReader.UnsuccessfulResponseCode
            };
}