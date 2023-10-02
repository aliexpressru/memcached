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

    internal override bool HasResult => Result is not null;

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
        return new GetCommand(Key);
    }
}