using Aer.Memcached.Client.Commands.Base;
using Aer.Memcached.Client.Commands.Enums;
using Aer.Memcached.Client.Commands.Extensions;
using Aer.Memcached.Client.Commands.Infrastructure;
using Aer.Memcached.Client.ConnectionPool;

namespace Aer.Memcached.Client.Commands;

internal class MultiDeleteCommand: MemcachedCommandBase
{
    private readonly IEnumerable<string> _keys;
    
    // this field exists as an optimization for subsequent lists creation
    // this is here due to allocation optimization for batch split case. Batches are IEnumerable<string>.
    // to not generate another collection in this case we simply pass keys count this command
    private readonly int _keysCount;
    private int _noopId;

    public MultiDeleteCommand(IEnumerable<string> keys, int keysCount, bool isAllowLongKeys): base(OpCode.DeleteQ)
    {
        _keys = isAllowLongKeys ? keys.Select(GetSafeLengthKey) : keys;
        _keysCount = keysCount;
    }

    internal override IList<ArraySegment<byte>> GetBuffer()
    {
        var keys = _keys;
        
        if (keys == null)
        {
            return Array.Empty<ArraySegment<byte>>();
        }

        var buffers = new List<ArraySegment<byte>>(_keysCount * 2); // get ops have 2 segments, header + key

        foreach (var key in keys)
        {
            var request = Build(key);

            request.CreateBuffer(buffers);
        }

        // uncork the server
        var noop = new BinaryRequest(OpCode.NoOp);
        _noopId = noop.CorrelationId;

        noop.CreateBuffer(buffers);

        return buffers;
    }

    protected override async Task<CommandResult> ReadResponseCoreAsync(PooledSocket socket, CancellationToken token)
    {
        var result = new CommandResult();

        ResponseReader = new BinaryResponseReader();

        while (await ResponseReader.ReadAsync(socket, token))
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