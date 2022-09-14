using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Extensions;
using Aer.Memcached.Client.ConnectionPool;

namespace Aer.Memcached.Client.Commands.Base;

internal abstract class SingleItemCommandBase: MemcachedCommandBase
{
    private string Key { get; }
    
    protected ulong CasValue { get; set; }

    protected SingleItemCommandBase(string key, OpCode opCode): base(opCode)
    {
        Key = key;
    }
    
    protected abstract CommandResult ProcessResponse(BinaryResponse response);

    protected abstract BinaryRequest Build(string key);

    public override IList<ArraySegment<byte>> GetBuffer()
    {
        return Build(Key).CreateBuffer();
    }

    public override CommandResult ReadResponse(PooledSocket socket)
    {
        Response = new BinaryResponse();
        var success = Response.Read(socket);

        CasValue = Response.Cas;
        StatusCode = Response.StatusCode;

        var result = new CommandResult
        {
            Success = success,
            Cas = CasValue,
            StatusCode = StatusCode
        };

        CommandResult responseResult;
        if (!(responseResult = ProcessResponse(Response)).Success)
        {
            result.InnerResult = responseResult;
            responseResult.Combine(result);
        }

        return result;
    }
}