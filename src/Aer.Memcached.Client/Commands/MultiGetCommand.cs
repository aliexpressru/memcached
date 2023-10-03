using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Extensions;
using Aer.Memcached.Client.Commands.Infrastructure;
using Aer.Memcached.Client.ConnectionPool;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Commands;

internal class MultiGetCommand: MemcachedCommandBase
{
    private readonly IEnumerable<string> _keys;
    
    // this field exists as an optimization for subsequent lists creation
    // this is here due to allocation optimization for batch split case. Batches are IEnumerable<string>.
    // to not generate another collection in this case we simply pass keys count to this command
    private readonly int _keysCount;
    private Dictionary<int, string> _idToKey;
    private int _noopId;
    
    // Not used for now, it is leaved here on purpose of further possible usage :)
    private Dictionary<string, ulong> _casValues;
    
    public Dictionary<string, CacheItemResult> Result { get; private set; }
    
    public MultiGetCommand(IEnumerable<string> keys, int keysCount): base(OpCode.GetQ)
    {
        _keys = keys;
        _keysCount = keysCount;
    }

    internal override bool HasResult => Result is {Count: > 0};

    internal override IList<ArraySegment<byte>> GetBuffer()
    {
        var keys = _keys;
        
        if (keys == null)
        {
            return Array.Empty<ArraySegment<byte>>();
        }
        
        // map the command's correlationId to the item key,
        // so we can use GetQ (which only returns the item data)
        _idToKey = new Dictionary<int, string>();

        var buffers = new List<ArraySegment<byte>>(_keysCount * 2); // get ops have 2 segments, header + key

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

    protected override CommandResult ReadResponseCore(PooledSocket socket)
    {
        Result = new Dictionary<string, CacheItemResult>();
        
        _casValues = new Dictionary<string, ulong>();
        
        var result = new CommandResult();

        ResponseReader = new BinaryResponseReader();
        
        while (ResponseReader.Read(socket))
        {
            if (ResponseReader.IsSocketDead)
            {
                return CommandResult.DeadSocket;
            }

            StatusCode = ResponseReader.StatusCode;

            // found the noop, quit
            if (ResponseReader.CorrelationId == _noopId)
            {
                return result.Pass();
            }

            // find the key to the response
            if (!_idToKey.TryGetValue(ResponseReader.CorrelationId, out var key))
            {
                // we're not supposed to get here tho
                throw new InvalidOperationException();
            }
            
            // deserialize the response
            int flags = BinaryConverter.DecodeInt32(ResponseReader.Extra, 0);

            Result[key] = new CacheItemResult((ushort)flags, ResponseReader.Data);
            _casValues[key] = ResponseReader.Cas;
        }

        // finished reading but we did not find the NOOP
        return result.Fail("Failed to find the end of operation marker");
    }

    internal override MultiGetCommand Clone()
    {
        return new MultiGetCommand(_keys, _keysCount);
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