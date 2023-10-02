using Aer.Memcached.Client.Commands.Enums;

namespace Aer.Memcached.Client.Commands.Infrastructure;

internal class BinaryRequest
{
    private const int MaxKeyLength = 250;
    private const ushort Reserved = 0;

    private readonly byte _operation;
    private static int _instanceCounter;

    public string Key { get; init; }

    public ulong Cas { get; init; }

    public ArraySegment<byte> Extra { get; init; }

    public ArraySegment<byte> Data { get; init; }

    public int CorrelationId { get; }

    public BinaryRequest(OpCode operation) : this((byte) operation)
    { }

    private BinaryRequest(byte commandCode)
    {
        _operation = commandCode;
        // session id
        CorrelationId = Interlocked.Increment(ref _instanceCounter);
    }

    public IList<ArraySegment<byte>> CreateBuffer(IList<ArraySegment<byte>> appendTo = null)
    {
        // key size 
        byte[] keyData = BinaryConverter.Encode(Key);
        int keyLength = keyData?.Length ?? 0;

        if (keyLength > MaxKeyLength)
        {
            throw new InvalidOperationException("KeyTooLong");
        }

        // extra size
        ArraySegment<byte> extras = Extra;
        int extraLength = extras.Array == null
            ? 0
            : extras.Count;
        
        if (extraLength > 0xff)
        {
            throw new InvalidOperationException("ExtraTooLong");
        }

        // body size
        ArraySegment<byte> body = Data;
        int bodyLength = body.Array == null
            ? 0
            : body.Count;

        // total payload size
        int totalLength = extraLength + keyLength + bodyLength;

        //build the header
        Span<byte> header = stackalloc byte[24];

        header[0x00] = 0x80; // magic
        header[0x01] = _operation;

        // key length
        header[0x02] = (byte) (keyLength >> 8);
        header[0x03] = (byte) (keyLength & 255);

        // extra length
        header[0x04] = (byte) (extraLength);

        // 5 -- data type, 0 (RAW)
        // 6,7 -- reserved, always 0

        header[0x06] = (byte) (Reserved >> 8);
        header[0x07] = (byte) (Reserved & 255);

        // body length
        header[0x08] = (byte) (totalLength >> 24);
        header[0x09] = (byte) (totalLength >> 16);
        header[0x0a] = (byte) (totalLength >> 8);
        header[0x0b] = (byte) (totalLength & 255);

        header[0x0c] = (byte) (CorrelationId >> 24);
        header[0x0d] = (byte) (CorrelationId >> 16);
        header[0x0e] = (byte) (CorrelationId >> 8);
        header[0x0f] = (byte) (CorrelationId & 255);

        // CAS
        ulong cas = Cas;

        if (cas > 0)
        {
            // skip this if no cas is specified
            header[0x10] = (byte) (cas >> 56);
            header[0x11] = (byte) (cas >> 48);
            header[0x12] = (byte) (cas >> 40);
            header[0x13] = (byte) (cas >> 32);
            header[0x14] = (byte) (cas >> 24);
            header[0x15] = (byte) (cas >> 16);
            header[0x16] = (byte) (cas >> 8);
            header[0x17] = (byte) (cas & 255);
        }

        var result = appendTo ?? new List<ArraySegment<byte>>(4);

        result.Add(new ArraySegment<byte>(header.ToArray()));

        if (extraLength > 0)
        {
            result.Add(extras);
        }

        // NOTE key must be already encoded and should not contain any invalid characters which are not allowed by the protocol
        if (keyLength > 0)
        {
            result.Add(new ArraySegment<byte>(keyData));
        }

        if (bodyLength > 0)
        {
            result.Add(body);
        }

        return result;
    }
}