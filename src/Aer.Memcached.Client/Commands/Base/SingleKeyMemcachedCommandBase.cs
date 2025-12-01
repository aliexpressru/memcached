using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Extensions;
using Aer.Memcached.Client.Commands.Infrastructure;
using Aer.Memcached.Client.ConnectionPool;

namespace Aer.Memcached.Client.Commands.Base;

internal abstract class SingleKeyMemcachedCommandBase: MemcachedCommandBase
{
    protected string Key { get; }

    protected ulong CasValue { get; set; }

    protected SingleKeyMemcachedCommandBase(string key, OpCode opCode, bool isAllowLongKeys) : base(opCode)
    {
        Key = isAllowLongKeys
            ? GetSafeLengthKey(key)
            : key;
    }

    protected abstract CommandResult ProcessResponse(BinaryResponseReader responseReader);

    protected abstract BinaryRequest Build(string key);

    internal override IList<ArraySegment<byte>> GetBuffer()
    {
        return Build(Key).CreateBuffer();
    }

    protected override async Task<CommandResult> ReadResponseCoreAsync(PooledSocket socket, CancellationToken token)
    {
        ResponseReader = new BinaryResponseReader();
        var success = await ResponseReader.ReadAsync(socket, token);

        if (ResponseReader.IsSocketDead)
        {
            return CommandResult.DeadSocket;
        }

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