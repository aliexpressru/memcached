using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Infrastructure;
using Aer.Memcached.Client.ConnectionPool;

namespace Aer.Memcached.Client.Commands.Base;

public abstract class MemcachedCommandBase: IDisposable
{
    internal BinaryResponse Response { get; set; }

    protected int StatusCode { get; set; }

    internal OpCode OpCode { get; }

    protected bool WasResponseRead { get; set; }

    protected MemcachedCommandBase(OpCode opCode)
    {
        OpCode = opCode;
    }

    internal CommandResult ReadResponse(PooledSocket socket)
    {
        var ret = ReadResponseCore(socket);
        WasResponseRead = true;

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

    public void SetResultFrom(MemcachedCommandBase source)
    {
        if (!WasResponseRead)
        {
            throw new InvalidOperationException(
                $"Can't set result from {source.GetType()}, the command was not executed yet.");
        }

        SetResultFromCore(source);
    }

    protected virtual void SetResultFromCore(MemcachedCommandBase source)
    {
        throw new NotSupportedException($"{nameof(SetResultFrom)} method is not supported for command of type {GetType()}");
    }

    public void Dispose()
    {
        Response?.Dispose();
    }
}