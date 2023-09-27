using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Extensions;
using Aer.Memcached.Client.Commands.Helpers;
using Aer.Memcached.Client.Commands.Infrastructure;

namespace Aer.Memcached.Client.Commands;

internal class DeleteCommand: SingleItemCommandBase
{
    public DeleteCommand(string key) : base(key, OpCode.Delete)
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
            return result.Pass();
        }

        CasValue = 0;

        var message = ResultHelper.ProcessResponseData(response.Data);
        return result.Fail(message);
    }
}