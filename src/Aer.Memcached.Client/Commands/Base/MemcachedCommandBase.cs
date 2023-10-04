using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Infrastructure;
using Aer.Memcached.Client.ConnectionPool;

namespace Aer.Memcached.Client.Commands.Base;

public abstract class MemcachedCommandBase: IDisposable
{
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

    internal CommandResult ReadResponse(PooledSocket socket)
    {
        var ret = ReadResponseCore(socket);

        return ret;
    }

    protected abstract CommandResult ReadResponseCore(PooledSocket socket);
    
    internal abstract IList<ArraySegment<byte>> GetBuffer();

    internal virtual MemcachedCommandBase Clone()
    {
        throw new NotSupportedException($"{nameof(Clone)} method is not supported for command of type {GetType()}.");
    }

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