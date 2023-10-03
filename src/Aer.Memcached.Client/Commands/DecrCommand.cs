using System.Buffers;
using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Extensions;
using Aer.Memcached.Client.Commands.Helpers;
using Aer.Memcached.Client.Commands.Infrastructure;

namespace Aer.Memcached.Client.Commands;

internal class DecrCommand: SingleItemCommandBase
{
    private readonly ulong _amountToSubtract;
    private readonly ulong _initialValue;
    private readonly uint _expiresAtUnixTimeSeconds;

    public ulong Result { get; private set; }

    public DecrCommand(string key, ulong amountToSubtract, ulong initialValue, uint expiresAtUnixTimeSeconds) : base(
        key,
        OpCode.Decrement)
    {
        _amountToSubtract = amountToSubtract;
        _initialValue = initialValue;
        _expiresAtUnixTimeSeconds = expiresAtUnixTimeSeconds;
    }

    protected override BinaryRequest Build(string key)
    {
        var extra = ArrayPool<byte>.Shared.Rent(20);
        try
        {
            var span = extra.AsSpan(0, 20);

            BinaryConverter.EncodeUInt64(_amountToSubtract, span, 0);
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

    protected override CommandResult ProcessResponse(BinaryResponseReader responseReader)
    {
        var result = new CommandResult();

        StatusCode = responseReader.StatusCode;
        if (responseReader.StatusCode == BinaryResponseReader.SuccessfulResponseCode)
        {
            Result = BinaryConverter.DecodeUInt64(responseReader.Data.Span, 0);
            
            return result.Pass();
        }

        var message = ResultHelper.ProcessResponseData(responseReader.Data);
        return result.Fail(message);
    }
}