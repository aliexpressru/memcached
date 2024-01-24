using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Extensions;
using Aer.Memcached.Client.Commands.Helpers;
using Aer.Memcached.Client.Commands.Infrastructure;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Commands;

internal class GetCommand: SingleKeyMemcachedCommandBase
{
    private readonly bool _isAllowLongKeys;
    
    internal override bool HasResult => Result is not null;
    
    public CacheItemResult Result { get; private set; }

    public GetCommand(string key, bool isAllowLongKeys) : base(key, OpCode.Get, isAllowLongKeys)
    {
        _isAllowLongKeys = isAllowLongKeys;
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

    internal override GetCommand Clone()
    {
        return new GetCommand(Key, _isAllowLongKeys);
    }
}