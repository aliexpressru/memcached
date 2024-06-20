using System.Diagnostics.CodeAnalysis;
using Aer.Memcached.Client.Commands.Base;

namespace Aer.Memcached.Client.Models;

[SuppressMessage("ReSharper", "MemberCanBeInternal")]
public class CommandExecutionResult : IDisposable
{
    /// <summary>
    /// Contains the executed command, since the actual result of the memcached command
    /// is stored on the command itself, this property is used to get the execution result.
    /// </summary>
    public MemcachedCommandBase ExecutedCommand { get; }

    /// <summary>
    /// Indicates whether the command execution was successful.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// If command execution was not successful, contains error message.
    /// </summary>
    public string ErrorMessage { get; init; }

    private CommandExecutionResult(MemcachedCommandBase executedCommand, bool isSuccess)
    {
        ExecutedCommand = executedCommand;
        Success = isSuccess;
    }

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
                $"Can't cast unsuccessfully executed command to '{typeof(T)}'. Only successfully executed commands can be cast to concrete types. Check {nameof(Success)} property before casting");
        }

        if (ExecutedCommand is T specificCommand)
        {
            return specificCommand;
        }

        throw new InvalidCastException(
            $"Can't cast command of type '{ExecutedCommand?.GetType()}' to type '{typeof(T)}'");
    }

    public static CommandExecutionResult Unsuccessful(MemcachedCommandBase executedCommand, string errorMessage)
        => new(executedCommand, false) {ErrorMessage = errorMessage};

    public static CommandExecutionResult Successful(MemcachedCommandBase executedCommand)
        => new(executedCommand, true);

    public void Dispose()
    {
        ExecutedCommand?.Dispose();
    }
}