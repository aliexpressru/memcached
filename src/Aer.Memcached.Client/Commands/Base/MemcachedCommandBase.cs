using System.Text;
using Aer.ConsistentHash;
using Aer.ConsistentHash.Abstractions;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Infrastructure;
using Aer.Memcached.Client.ConnectionPool;

namespace Aer.Memcached.Client.Commands.Base;

public abstract class MemcachedCommandBase : IDisposable
{
    /// <summary>
    /// The memcached by-design key length limitation.
    /// </summary>
    public const int MemcachedKeyLengthMaxLengthBytes = 250;

    private static readonly IHashCalculator HashCalculator = new HashCalculator();

    private bool _isDisposed;

    internal BinaryResponseReader ResponseReader { get; set; }

    protected int StatusCode { get; set; }

    internal OpCode OpCode { get; }

    /// <summary>
    /// Indicates, whether this command has a non-null result.
    /// </summary>
    internal virtual bool HasResult =>
        throw new NotSupportedException(
            $"{nameof(HasResult)} property is not supported for command of type {GetType()}.");

    protected MemcachedCommandBase(OpCode opCode)
    {
        OpCode = opCode;
    }

    internal async Task<CommandResult> ReadResponseAsync(PooledSocket socket, CancellationToken token = default)
    {
        var ret = await ReadResponseCoreAsync(socket, token);

        return ret;
    }

    protected abstract Task<CommandResult> ReadResponseCoreAsync(PooledSocket socket, CancellationToken token = default);

    internal abstract IList<ArraySegment<byte>> GetBuffer();

    internal virtual MemcachedCommandBase Clone() =>
        throw new NotSupportedException(
            $"{nameof(Clone)} method is not supported for command of type {GetType()}.");


    internal static string GetSafeLengthKey(string possiblyTooLongKey) =>
        Encoding.UTF8.GetByteCount(possiblyTooLongKey) > MemcachedKeyLengthMaxLengthBytes
            ? HashCalculator.DigestValue(possiblyTooLongKey)
            : possiblyTooLongKey;

    public override string ToString()
    {
        return OpCode.ToString();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        // dispose of response reader to return all underlying rented buffers
        ResponseReader?.Dispose();
        _isDisposed = true;
    }
}