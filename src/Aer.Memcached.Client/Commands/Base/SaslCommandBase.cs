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
        var reader = new BinaryResponseReader();

        var success = reader.Read(socket);

        if (ResponseReader.IsSocketDead)
        {
            return CommandResult.DeadSocket;
        }

        StatusCode = reader.StatusCode;
        Data = reader.Data;

        var result = new CommandResult
        {
            StatusCode = StatusCode
        };

        if (success && !reader.IsSocketDead)
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