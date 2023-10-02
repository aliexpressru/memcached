using System.Buffers;
using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Extensions;
using Aer.Memcached.Client.Commands.Helpers;
using Aer.Memcached.Client.Commands.Infrastructure;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Commands;

internal class StoreCommand: SingleItemCommandBase
{
    private readonly CacheItemForRequest _cacheItem;
    private readonly uint _expiresAtUnixTimeSeconds;

    public StoreCommand(StoreMode storeMode, string key, CacheItemForRequest cacheItem, uint expiresAtUnixTimeSeconds) : base(key, storeMode.Resolve())
    {
        _cacheItem = cacheItem;
        _expiresAtUnixTimeSeconds = expiresAtUnixTimeSeconds;
    }

    protected override BinaryRequest Build(string key)
    {
        var extra = ArrayPool<byte>.Shared.Rent(8);
        try
        {
            var span = extra.AsSpan(0, 8);

            BinaryConverter.EncodeUInt32(_cacheItem.Flags, span, 0);
            BinaryConverter.EncodeUInt32(_expiresAtUnixTimeSeconds, span, 4);

            var request = new BinaryRequest(OpCode)
            {
                Key = key,
                Cas = CasValue,
                Extra = new ArraySegment<byte>(span.ToArray()),
                Data = _cacheItem.Data
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
            return result.Pass();
        }

        var message = ResultHelper.ProcessResponseData(responseReader.Data);
        return result.Fail(message);
    }
}