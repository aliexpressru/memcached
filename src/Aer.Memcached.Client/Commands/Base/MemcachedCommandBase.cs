using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.ConnectionPool;

namespace Aer.Memcached.Client.Commands.Base;

public abstract class MemcachedCommandBase: IDisposable
{
    protected int StatusCode { get; set; }

    public abstract CommandResult ReadResponse(PooledSocket socket);
    
    public abstract IList<ArraySegment<byte>> GetBuffer();

    protected BinaryResponse Response;

    public OpCode OpCode { get; }

    protected MemcachedCommandBase(OpCode opCode)
    {
        OpCode = opCode;
    }

    public void Dispose()
    {
        Response?.Dispose();
    }
}