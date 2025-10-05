using System.Buffers;
using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Extensions;
using Aer.Memcached.Client.Commands.Helpers;
using Aer.Memcached.Client.Commands.Infrastructure;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Commands;

internal class GetAndTouchCommand : SingleKeyMemcachedCommandBase
{
    private readonly bool _isAllowLongKeys;
    private readonly uint _expiresAtUnixTimeSeconds;

    internal override bool HasResult => Result is not null;

    public CacheItemResult Result { get; private set; }

    public GetAndTouchCommand(string key, uint expiresAtUnixTimeSeconds, bool isAllowLongKeys) : base(key, OpCode.GetAndTouch,
        isAllowLongKeys)
    {
        _isAllowLongKeys = isAllowLongKeys;
        _expiresAtUnixTimeSeconds = expiresAtUnixTimeSeconds;
    }

    protected override BinaryRequest Build(string key)
    {
        var extra = ArrayPool<byte>.Shared.Rent(4);
        try
        {
            var span = extra.AsSpan(0, 4);

            BinaryConverter.EncodeUInt32(_expiresAtUnixTimeSeconds, span, 0);

            var request = new BinaryRequest(OpCode)
            {
                Key = key,
                Cas = CasValue,                
                Extra = new ArraySegment<byte>(span.ToArray()),
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

    internal override GetAndTouchCommand Clone()
    {
        return new GetAndTouchCommand(Key, _expiresAtUnixTimeSeconds, _isAllowLongKeys);
    }
}