using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Infrastructure;
using Aer.Memcached.Client.ConnectionPool;

namespace Aer.Memcached.Client.Commands.Base;

public abstract class MemcachedCommandBase: IDisposable
{
    internal BinaryResponseReader ResponseReader { get; set; }

    protected int StatusCode { get; set; }

    internal OpCode OpCode { get; }

    protected bool WasResponseRead { get; set; }

    protected MemcachedCommandBase(OpCode opCode)
    {
        OpCode = opCode;
    }

    internal CommandResult ReadResponse(PooledSocket socket)
    {
        // we set this flag before the actual read so that if we 
        // fail to read result we still consider the result read
        WasResponseRead = true;
        
        var ret = ReadResponseCore(socket);

        return ret;
    }

    protected abstract CommandResult ReadResponseCore(PooledSocket socket);
    
    internal abstract IList<ArraySegment<byte>> GetBuffer();

    public MemcachedCommandBase Clone()
    {
        if (WasResponseRead)
        {
            throw new InvalidOperationException($"Can't clone {GetType()} after it has been executed.");
        }

        return CloneCore();
    }

    protected virtual MemcachedCommandBase CloneCore()
    {
        throw new NotSupportedException($"{nameof(Clone)} method is not supported for command of type {GetType()}.");
    }

    public bool TrySetResultFrom(MemcachedCommandBase source)
    {
        if (!source.WasResponseRead)
        {
            throw new InvalidOperationException(
                $"Can't set result from {source.GetType()}, the command was not executed yet.");
        }

        // the result is not set if source result is null
        var wasResultSuccessfullySet = TrySetResultFromCore(source);

        return wasResultSuccessfullySet;
    }

    protected virtual bool TrySetResultFromCore(MemcachedCommandBase source)
    {
        throw new NotSupportedException($"{nameof(TrySetResultFrom)} method is not supported for command of type {GetType()}");
    }

    public override string ToString()
    {
        return OpCode.ToString();
    }

    public void Dispose()
    {
        // dispose of response reader to return all underlying rented buffers
        ResponseReader?.Dispose();
    }
}