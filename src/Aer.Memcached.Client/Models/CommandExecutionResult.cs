using Aer.Memcached.Client.Commands.Base;

namespace Aer.Memcached.Client.Models;

public class CommandExecutionResult : IDisposable
{
    /// <summary>
    /// Contains executed command, since the actual result of the memcached command is located on the command itself.
    /// </summary>
    public MemcachedCommandBase ExecutedCommand { set; get; }

    /// <summary>
    /// Indicates whether the command execution was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the encapsulated command as spcific concrete command type.
    /// </summary>
    /// <typeparam name="T">The concrete command type to get.</typeparam>
    public T GetCommandAs<T>()
        where T : MemcachedCommandBase
    {
        if (!Success)
        {
            throw new InvalidCastException(
                $"Can't cast unsuccessfully executed command to '{typeof(T)}'. Check if command {nameof(Success)} property is 'True' before getting command of concrete type.");
        }

        if (ExecutedCommand is T specificCommand)
        {
            return specificCommand;
        }

        throw new InvalidCastException($"Can't cast command of type '{ExecutedCommand?.GetType()}' to type '{typeof(T)}'");
    }

    public static CommandExecutionResult Unsuccessful { get; } = new()
    {
        Success = false
    };

    public static CommandExecutionResult Successful(MemcachedCommandBase executedCommand)
    {
        return new ()
        {
            ExecutedCommand = executedCommand,
            Success = true
        };
    }

    public void Dispose()
    {
        ExecutedCommand?.Dispose();
    }
}