using System.Buffers;
using Aer.Memcached.Client.ConnectionPool;

namespace Aer.Memcached.Client.Commands.Infrastructure;

/// <summary>
/// A class for reading responde data from memcached socket and temporary buffers storage.
/// </summary>
internal class BinaryResponseReader: IDisposable
{
    private const byte MagicValue = 0x81;
    private const int HeaderLength = 24;

    private const int HeaderOpcode = 1;
    private const int HeaderKey = 2; // 2-3
    private const int HeaderExtra = 4;
    private const int HeaderDatatype = 5;
    private const int HeaderStatus = 6; // 6-7
    private const int HeaderBody = 8; // 8-11
    private const int HeaderOpaque = 12; // 12-15
    private const int HeaderCas = 16; // 16-23

    public const int SuccessfulResponseCode = 0;
    
    public const int UnsuccessfulResponseCode = 1;

    private readonly Queue<byte[]> _rentedBufferForData = new();
    
    public int StatusCode { get; private set; } = -1;

    public bool IsSocketDead { get; private set; }

    public int CorrelationId { get; private set; }
    
    public ulong Cas { get; private set; }

    public ReadOnlyMemory<byte> Extra { get; private set; }
    
    public ReadOnlyMemory<byte> Data { get; private set; }
    
    public byte Opcode { get; private set; }
    
    public int KeyLength { get; private set; }
    
    public byte DataType { get; private set; }

    public bool Read(PooledSocket socket)
    {
        StatusCode = -1;

        if (!socket.IsAlive)
        {
            // We return True here if underlying socket is considered to be dead.
            // This is done to prevent infinite looping on the dead socket whikle reading from it.
            
            IsSocketDead = true;
            return true;
        }
        
        var header = ArrayPool<byte>.Shared.Rent(HeaderLength);
        int dataLength;
        int extraLength;
        
        try
        {
            var span = header.AsSpan(0, HeaderLength);
            socket.Read(span, HeaderLength);
            DeserializeHeader(span, out dataLength, out extraLength);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(header);
        }

        if (dataLength > 0)
        {
            var bufferData = ArrayPool<byte>.Shared.Rent(dataLength);
            var span = bufferData.AsSpan(0, dataLength);
            socket.Read(span, dataLength);

            Extra = new ReadOnlyMemory<byte>(bufferData, 0, extraLength);
            Data = new ReadOnlyMemory<byte>(bufferData, extraLength, dataLength - extraLength);
            
            _rentedBufferForData.Enqueue(bufferData);
        }

        return StatusCode == SuccessfulResponseCode;
    }

    public Memory<byte> GetMemoryBuffer(int dataLength)
    {
        var bufferData = ArrayPool<byte>.Shared.Rent(dataLength);
        var memory = bufferData.AsMemory(0, dataLength);
        
        _rentedBufferForData.Enqueue(bufferData);

        return memory;
    }

    private void DeserializeHeader(Span<byte> spanHeader, out int dataLength, out int extraLength)
    {
        if (spanHeader[0] != MagicValue)
        {
            throw new InvalidOperationException("Expected magic value " + MagicValue + ", received: " + spanHeader[0]);
        }
        
        StatusCode = BinaryConverter.DecodeUInt16(spanHeader, HeaderStatus);
        KeyLength = BinaryConverter.DecodeUInt16(spanHeader, HeaderKey);
        CorrelationId = BinaryConverter.DecodeInt32(spanHeader, HeaderOpaque);
        dataLength = BinaryConverter.DecodeInt32(spanHeader, HeaderBody);
        Cas = BinaryConverter.DecodeUInt64(spanHeader, HeaderCas);
        DataType = spanHeader[HeaderDatatype];
        Opcode = spanHeader[HeaderOpcode];
        extraLength = spanHeader[HeaderExtra];
    }

    public void Dispose()
    {
        ReturnRentedBuffer();
    }

    private void ReturnRentedBuffer()
    {
        if(_rentedBufferForData == null)
        {
            return;
        }

        while (_rentedBufferForData.TryDequeue(out var rentedData))
        {
            ArrayPool<byte>.Shared.Return(rentedData);
        }
    }
}