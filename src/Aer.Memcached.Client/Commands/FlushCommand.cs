using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.ConnectionPool;

namespace Aer.Memcached.Client.Commands;

internal class FlushCommand: MemcachedCommandBase
{
    public FlushCommand() : base(OpCode.Flush)
    {
    }
    
    public override CommandResult ReadResponse(PooledSocket socket)
    {
        Response = new BinaryResponse();
        var success = Response.Read(socket);
        if (success)
        {
            return new CommandResult
            {
                Success = true,
                StatusCode = BinaryResponse.SuccessfulResponseCode
            };
        }

        return new CommandResult
        {
            Success = false,
            StatusCode = BinaryResponse.UnsuccessfulResponseCode,
        };
    }

    public override IList<ArraySegment<byte>> GetBuffer()
    {
        return Build().CreateBuffer();
    }
    
    private BinaryRequest Build() => new(OpCode);

}