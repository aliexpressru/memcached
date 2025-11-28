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
    
    protected override async Task<CommandResult> ReadResponseCoreAsync(PooledSocket socket, CancellationToken token = default)
    {
        ResponseReader = new BinaryResponseReader();
        var success = await ResponseReader.ReadAsync(socket, token);

        if (ResponseReader.IsSocketDead)
        {
            return CommandResult.DeadSocket;
        }
        
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