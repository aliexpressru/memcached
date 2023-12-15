using System.Text;

namespace Aer.Memcached.Client.Commands.Infrastructure;

internal static class BinaryConverter
{
    public static ushort DecodeUInt16(Span<byte> span, int offset)
    {
        var offsetSpan = span[offset..];

        return (ushort) ((offsetSpan[0] << 8) + offsetSpan[0]);
    }

    public static int DecodeInt32(ReadOnlyMemory<byte> segment, int offset)
    {
        return segment.IsEmpty
            ? default
            : DecodeInt32(segment.Span, offset);
    }

    public static int DecodeInt32(Span<byte> span, int offset)
    {
        var offsetSpan = span[offset..];

        return (offsetSpan[0] << 24) | (offsetSpan[1] << 16) | (offsetSpan[2] << 8) | offsetSpan[3];
    }

    private static int DecodeInt32(ReadOnlySpan<byte> span, int offset)
    {
        var offsetSpan = span[offset..];

        return (offsetSpan[0] << 24) | (offsetSpan[1] << 16) | (offsetSpan[2] << 8) | offsetSpan[3];
    }

    public static ulong DecodeUInt64(ReadOnlySpan<byte> span, int offset)
    {
        var offsetSpan = span[offset..];

        var part1 = (uint) ((offsetSpan[0] << 24) | (offsetSpan[1] << 16) | (offsetSpan[2] << 8) | offsetSpan[3]);
        var part2 = (uint) ((offsetSpan[4] << 24) | (offsetSpan[5] << 16) | (offsetSpan[6] << 8) | offsetSpan[7]);

        return ((ulong) part1 << 32) | part2;
    }

    public static void EncodeUInt32(uint value, Span<byte> span, int offset)
    {
        var offsetSpan = span[offset..];

        offsetSpan[0] = (byte) (value >> 24);
        offsetSpan[1] = (byte) (value >> 16);
        offsetSpan[2] = (byte) (value >> 8);
        offsetSpan[3] = (byte) (value & 255);
    }

    public static void EncodeUInt64(ulong value, Span<byte> span, int offset)
    {
        var offsetSpan = span[offset..];

        offsetSpan[0] = (byte) (value >> 56);
        offsetSpan[1] = (byte) (value >> 48);
        offsetSpan[2] = (byte) (value >> 40);
        offsetSpan[3] = (byte) (value >> 32);
        offsetSpan[4] = (byte) (value >> 24);
        offsetSpan[5] = (byte) (value >> 16);
        offsetSpan[6] = (byte) (value >> 8);
        offsetSpan[7] = (byte) (value & 255);
    }

    public static byte[] Encode(string value)
    {
        return string.IsNullOrEmpty(value)
            ? null
            : Encoding.UTF8.GetBytes(value);
    }
}