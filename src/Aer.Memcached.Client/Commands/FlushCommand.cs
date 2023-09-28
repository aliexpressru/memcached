using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Infrastructure;
using Aer.Memcached.Client.ConnectionPool;

namespace Aer.Memcached.Client.Commands;

internal class FlushCommand: MemcachedCommandBase
{
    public FlushCommand() : base(OpCode.Flush)
    {
    }
    
    protected override CommandResult ReadResponseCore(PooledSocket socket)
    {
        ResponseReader = new BinaryResponseReader();
        var success = ResponseReader.Read(socket);
        if (success)
        {
            return new CommandResult
            {
                Success = true,
                StatusCode = BinaryResponseReader.SuccessfulResponseCode
            };
        }

        return new CommandResult
        {
            Success = false,
            StatusCode = BinaryResponseReader.UnsuccessfulResponseCode,
        };
    }

    internal override IList<ArraySegment<byte>> GetBuffer()
    {
        return Build().CreateBuffer();
    }
    
    private BinaryRequest Build() => new(OpCode);

}