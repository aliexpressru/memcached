using System.Buffers;
using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Extensions;
using Aer.Memcached.Client.Commands.Helpers;
using Aer.Memcached.Client.Commands.Infrastructure;
using Aer.Memcached.Client.ConnectionPool;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Commands;

internal class MultiStoreCommand: MemcachedCommandBase
{
    private readonly Dictionary<string, CacheItemForRequest> _keyValues;
    private readonly Dictionary<string, uint> _expirationByKey;
    private readonly bool _isAllowLongKeys;
    private int _noopId;

    public MultiStoreCommand(
        StoreMode storeMode, 
        Dictionary<string, CacheItemForRequest> keyValues, 
        Dictionary<string, uint> expirationByKey,
        bool isAllowLongKeys): base(storeMode.Resolve())
    {
        _keyValues = keyValues;
        _expirationByKey = expirationByKey;
        _isAllowLongKeys = isAllowLongKeys;
    }

    internal override IList<ArraySegment<byte>> GetBuffer()
    {
        if (_keyValues == null || _keyValues.Count == 0)
        {
            return Array.Empty<ArraySegment<byte>>();
        }
        
        // set ops have 3 segments, header + key + body
        var buffers = new List<ArraySegment<byte>>(_keyValues.Count * 3);
        
        foreach (var keyValue in _keyValues)
        {
            var request = Build(keyValue.Key, keyValue.Value, 0);

            request.CreateBuffer(buffers);
        }

        // uncork the server
        var noop = new BinaryRequest(OpCode.NoOp);
        _noopId = noop.CorrelationId;

        noop.CreateBuffer(buffers);

        return buffers;
    }

    protected override async Task<CommandResult> ReadResponseCoreAsync(PooledSocket socket, CancellationToken token = default)
    {
        var result = new CommandResult();

        ResponseReader = new BinaryResponseReader();

        while (await ResponseReader.ReadAsync(socket, token))
        {
            if (ResponseReader.IsSocketDead)
            {
                return CommandResult.DeadSocket;
            }
            
            if (ResponseReader.StatusCode != BinaryResponseReader.SuccessfulResponseCode)
            {
                var message = ResultHelper.ProcessResponseData(ResponseReader.Data);
                return result.Fail(message);
            }
            
            StatusCode = ResponseReader.StatusCode;

            // found the noop, quit
            if (ResponseReader.CorrelationId == _noopId)
            {
                return result.Pass();
            }
        }

        // finished reading but we did not find the NOOP
        return result.Fail("Failed to find the end of operations");
    }
    
    private BinaryRequest Build(string key, CacheItemForRequest cacheItem, ulong casValue)
    {
        var extra = ArrayPool<byte>.Shared.Rent(8);
        try
        {
            var span = extra.AsSpan(0, 8);

            BinaryConverter.EncodeUInt32(cacheItem.Flags, span, 0);
            BinaryConverter.EncodeUInt32(_expirationByKey[key], span, 4);

            var request = new BinaryRequest(OpCode)
            {
                Key = _isAllowLongKeys ? GetSafeLengthKey(key) : key,
                Cas = casValue,
                Extra = new ArraySegment<byte>(span.ToArray()),
                Data = cacheItem.Data
            };

            return request;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(extra);
        }
    }
}