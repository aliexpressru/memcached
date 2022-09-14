using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Extensions;
using Aer.Memcached.Client.Commands.Helpers;
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

    protected override CommandResult ProcessResponse(BinaryResponse response)
    {
        var status = response.StatusCode;
        var result = new CommandResult();

        StatusCode = status;

        if (status == BinaryResponse.SuccessfulResponseCode)
        {
            int flags = BinaryConverter.DecodeInt32(response.Extra, 0);
            Result = new CacheItemResult((ushort)flags, response.Data);
            CasValue = response.Cas;

            return result.Pass();
        }

        CasValue = 0;

        var message = ResultHelper.ProcessResponseData(response.Data);
        return result.Fail(message);
    }
}