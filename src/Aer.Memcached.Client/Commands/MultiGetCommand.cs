using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Extensions;
using Aer.Memcached.Client.ConnectionPool;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Commands;

internal class MultiGetCommand: MemcachedCommandBase
{
    private readonly IList<string> _keys;
    private Dictionary<int, string> _idToKey;
    private int _noopId;
    
    // Not used for now, it is leaved here on purpose of further possible usage :)
    private Dictionary<string, ulong> _casValues;
    
    public Dictionary<string, CacheItemResult> Result { get; private set; }
    
    public MultiGetCommand(IList<string> keys): base(OpCode.GetQ)
    {
        _keys = keys;
    }

    public override IList<ArraySegment<byte>> GetBuffer()
    {
        var keys = _keys;

        if (keys == null || keys.Count == 0)
        {
            return Array.Empty<ArraySegment<byte>>();
        }

        // map the command's correlationId to the item key,
        // so we can use GetQ (which only returns the item data)
        _idToKey = new Dictionary<int, string>();

        // get ops have 2 segments, header + key
        var buffers = new List<ArraySegment<byte>>(keys.Count * 2);

        foreach (var key in keys)
        {
            var request = Build(key);

            request.CreateBuffer(buffers);

            // we use this to map the responses to the keys
            _idToKey[request.CorrelationId] = key;
        }

        // uncork the server
        var noop = new BinaryRequest(OpCode.NoOp);
        _noopId = noop.CorrelationId;

        noop.CreateBuffer(buffers);

        return buffers;
    }

    public override CommandResult ReadResponse(PooledSocket socket)
    {
        Result = new Dictionary<string, CacheItemResult>();
        _casValues = new Dictionary<string, ulong>();
        var result = new CommandResult();

        Response = new BinaryResponse();

        while (Response.Read(socket))
        {
            StatusCode = Response.StatusCode;

            // found the noop, quit
            if (Response.CorrelationId == _noopId)
            {
                return result.Pass();
            }

            // find the key to the response
            if (!_idToKey.TryGetValue(Response.CorrelationId, out var key))
            {
                // we're not supposed to get here tho
                throw new InvalidOperationException();
            }
            
            // deserialize the response
            int flags = BinaryConverter.DecodeInt32(Response.Extra, 0);

            Result[key] = new CacheItemResult((ushort)flags, Response.Data);
            _casValues[key] = Response.Cas;
        }

        // finished reading but we did not find the NOOP
        return result.Fail("Failed to find the end of operations");
    }
    
    private BinaryRequest Build(string key)
    {
        var request = new BinaryRequest(OpCode)
        {
            Key = key
        };

        return request;
    }
}