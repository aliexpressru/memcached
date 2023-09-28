using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Extensions;
using Aer.Memcached.Client.Commands.Infrastructure;
using Aer.Memcached.Client.ConnectionPool;

namespace Aer.Memcached.Client.Commands.Base;

internal abstract class SingleItemCommandBase: MemcachedCommandBase
{
    protected string Key { get; }
    
    protected ulong CasValue { get; set; }

    protected SingleItemCommandBase(string key, OpCode opCode): base(opCode)
    {
        Key = key;
    }
    
    protected abstract CommandResult ProcessResponse(BinaryResponseReader responseReader);

    protected abstract BinaryRequest Build(string key);

    internal override IList<ArraySegment<byte>> GetBuffer()
    {
        return Build(Key).CreateBuffer();
    }

    protected override CommandResult ReadResponseCore(PooledSocket socket)
    {
        ResponseReader = new BinaryResponseReader();
        var success = ResponseReader.Read(socket);

        CasValue = ResponseReader.Cas;
        StatusCode = ResponseReader.StatusCode;

        var result = new CommandResult
        {
            Success = success,
            Cas = CasValue,
            StatusCode = StatusCode
        };

        CommandResult responseResult;
        if (!(responseResult = ProcessResponse(ResponseReader)).Success)
        {
            result.InnerResult = responseResult;
            responseResult.Combine(result);
        }

        return result;
    }
}