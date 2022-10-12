using System.Buffers;
using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Extensions;
using Aer.Memcached.Client.Commands.Helpers;

namespace Aer.Memcached.Client.Commands;

internal class IncrCommand: SingleItemCommandBase
{
    private readonly ulong _amountToAdd;
    private readonly ulong _initialValue;
    private readonly uint _expiresAtUnixTimeSeconds;

    public ulong Result { get; private set; }

    public IncrCommand(string key, ulong amountToAdd, ulong initialValue, uint expiresAtUnixTimeSeconds) : base(key, OpCode.Increment)
    {
        _amountToAdd = amountToAdd;
        _initialValue = initialValue;
        _expiresAtUnixTimeSeconds = expiresAtUnixTimeSeconds;
    }

    protected override BinaryRequest Build(string key)
    {
        var extra = ArrayPool<byte>.Shared.Rent(20);
        try
        {
            var span = extra.AsSpan(0, 20);

            BinaryConverter.EncodeUInt64(_amountToAdd, span, 0);
            BinaryConverter.EncodeUInt64(_initialValue, span, 8);
            BinaryConverter.EncodeUInt32(_expiresAtUnixTimeSeconds, span, 16);

            var request = new BinaryRequest(OpCode)
            {
                Key = key,
                Extra = new ArraySegment<byte>(span.ToArray())
            };

            return request;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(extra);
        }
    }

    protected override CommandResult ProcessResponse(BinaryResponse response)
    {
        var result = new CommandResult();

        StatusCode = response.StatusCode;
        if (response.StatusCode == BinaryResponse.SuccessfulResponseCode)
        {
            Result = BinaryConverter.DecodeUInt64(response.Data.Span, 0);
            
            return result.Pass();
        }

        var message = ResultHelper.ProcessResponseData(response.Data);
        return result.Fail(message);
    }
}