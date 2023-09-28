using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Extensions;
using Aer.Memcached.Client.Commands.Helpers;
using Aer.Memcached.Client.Commands.Infrastructure;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Commands;

internal class GetCommand: SingleItemCommandBase
{
    public CacheItemResult Result { get; private set; }

    public GetCommand(string key) : base(key, OpCode.Get)
    {
    }

    protected override BinaryRequest Build(string key)
    {
        var request = new BinaryRequest(OpCode)
        {
            Key = key,
            Cas = CasValue
        };

        return request;
    }

    protected override CommandResult ProcessResponse(BinaryResponseReader responseReader)
    {
        var status = responseReader.StatusCode;
        var result = new CommandResult();

        StatusCode = status;

        if (status == BinaryResponseReader.SuccessfulResponseCode)
        {
            int flags = BinaryConverter.DecodeInt32(responseReader.Extra, 0);
            Result = new CacheItemResult((ushort)flags, responseReader.Data);
            CasValue = responseReader.Cas;

            return result.Pass();
        }

        CasValue = 0;

        var message = ResultHelper.ProcessResponseData(responseReader.Data);
        return result.Fail(message);
    }

    protected override GetCommand CloneCore()
    {
        return new GetCommand(Key);
    }

    protected override bool TrySetResultFromCore(MemcachedCommandBase source)
    {
        if (source is not GetCommand gc)
        {
            throw new InvalidOperationException($"Can't set result of {GetType()} from {source.GetType()}");
        }

        if (gc.Result is null)
        { 
            // can't set result - source result is null
            return false;
        }

        // since the source command will be disposed of along with its ResponseReader
        // we create a new reader for temporary buffer storage
        ResponseReader = new BinaryResponseReader(); 
        
        Result = gc.Result.Clone(ResponseReader);
        StatusCode = gc.StatusCode;
        CasValue = gc.CasValue;

        return true;
    }
}