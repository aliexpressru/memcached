using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Extensions;
using Aer.Memcached.Client.Commands.Infrastructure;
using Aer.Memcached.Client.ConnectionPool;

namespace Aer.Memcached.Client.Commands.Base;

internal abstract class SaslCommandBase: MemcachedCommandBase
{
    protected const string SaslMechanism = "PLAIN";
    
    internal ReadOnlyMemory<byte> Data { get; private set; }
    
    protected SaslCommandBase(OpCode opCode) : base(opCode)
    {
    }

    protected override CommandResult ReadResponseCore(PooledSocket socket)
    {
        ResponseReader = new BinaryResponseReader();

        var success = ResponseReader.Read(socket);

        if (ResponseReader.IsSocketDead)
        {
            return CommandResult.DeadSocket;
        }

        StatusCode = ResponseReader.StatusCode;
        Data = ResponseReader.Data;

        var result = new CommandResult
        {
            StatusCode = StatusCode
        };

        if (success && !ResponseReader.IsSocketDead)
        {
            result.Pass();
        }
        else
        {
            result.Fail("Failed to read response");
        }
        
        return result;
    }
}