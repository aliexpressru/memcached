using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Extensions;
using Aer.Memcached.Client.ConnectionPool;

namespace Aer.Memcached.Client.Commands.Base;

public abstract class SaslCommandBase: MemcachedCommandBase
{
    protected const string SaslMechanism = "PLAIN";
    
    public ReadOnlyMemory<byte> Data { get; private set; }
    
    protected SaslCommandBase(OpCode opCode) : base(opCode)
    {
    }
    
    public override CommandResult ReadResponse(PooledSocket socket)
    {
        var response = new BinaryResponse();

        var success = response.Read(socket);

        StatusCode = response.StatusCode;
        Data = response.Data;

        var result = new CommandResult
        {
            StatusCode = StatusCode
        };

        if (success)
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