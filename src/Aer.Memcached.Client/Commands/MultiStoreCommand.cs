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
    private readonly uint _expires;
    private int _noopId;

    public MultiStoreCommand(
        StoreMode storeMode, 
        Dictionary<string, CacheItemForRequest> keyValues, 
        uint expires): base(storeMode.Resolve())
    {
        _keyValues = keyValues;
        _expires = expires;
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

    protected override CommandResult ReadResponseCore(PooledSocket socket)
    {
        var result = new CommandResult();

        ResponseReader = new BinaryResponseReader();

        while (ResponseReader.Read(socket))
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
            BinaryConverter.EncodeUInt32(_expires, span, 4);

            var request = new BinaryRequest(OpCode)
            {
                Key = key,
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