namespace Aer.Memcached.Client.Commands;

public class CommandResult
{
    /// <summary>
    /// A value indicating whether an command was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// A message indicating success, warning or failure reason for an command
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// An exception that caused a failure
    /// </summary>
    public Exception Exception { get; set; }

    /// <summary>
    /// The StatusCode returned from the server
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// A result that influenced the current result
    /// </summary>
    public CommandResult InnerResult { get; set; }
    
    public ulong Cas { get; set; }
}