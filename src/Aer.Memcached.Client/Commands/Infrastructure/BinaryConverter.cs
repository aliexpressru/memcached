using System.Text;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Commands.Infrastructure;

internal static class BinaryConverter
{
    private const uint RawDataFlag = 0xfa52;
    private const uint TypeCodeSerializationMask = 0x0100;
    private const uint TypeCodeDeserializationMask = 0xff;

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

    public static CacheItemForRequest Serialize(object value, IObjectBinarySerializer objectBinarySerializer)
    {
        if (value == null)
        {
            return new CacheItemForRequest(TypeCodeToFlag(TypeCode.Empty), new ArraySegment<byte>(Array.Empty<byte>()));
        }

        switch (value)
        {
            case ArraySegment<byte> arraySegment:
                return new CacheItemForRequest(RawDataFlag, arraySegment);
            case byte[] byteArray:
                return new CacheItemForRequest(RawDataFlag, byteArray);
        }

        var typeCode = Type.GetTypeCode(value.GetType());
        ArraySegment<byte> data;

        switch (typeCode)
        {
            case TypeCode.Empty:
                data = new ArraySegment<byte>(Array.Empty<byte>());
                break;
            case TypeCode.DBNull:
                data = new ArraySegment<byte>(Array.Empty<byte>());
                break;
            case TypeCode.String:
                data = new ArraySegment<byte>(Encode(value.ToString()) ?? Array.Empty<byte>());
                break;
            case TypeCode.Boolean:
                data = new ArraySegment<byte>(BitConverter.GetBytes((bool) value));
                break;
            case TypeCode.SByte:
                data = new ArraySegment<byte>(BitConverter.GetBytes((sbyte) value));
                break;
            case TypeCode.Byte:
                data = new ArraySegment<byte>(BitConverter.GetBytes((byte) value));
                break;
            case TypeCode.Int16:
                data = new ArraySegment<byte>(BitConverter.GetBytes((short) value));
                break;
            case TypeCode.Int32:
                data = new ArraySegment<byte>(BitConverter.GetBytes((int) value));
                break;
            case TypeCode.Int64:
                data = new ArraySegment<byte>(BitConverter.GetBytes((long) value));
                break;
            case TypeCode.UInt16:
                data = new ArraySegment<byte>(BitConverter.GetBytes((ushort) value));
                break;
            case TypeCode.UInt32:
                data = new ArraySegment<byte>(BitConverter.GetBytes((uint) value));
                break;
            case TypeCode.UInt64:
                data = new ArraySegment<byte>(BitConverter.GetBytes((ulong) value));
                break;
            case TypeCode.Char:
                data = new ArraySegment<byte>(BitConverter.GetBytes((char) value));
                break;
            case TypeCode.DateTime:
                data = new ArraySegment<byte>(BitConverter.GetBytes(((DateTime) value).ToBinary()));
                break;
            case TypeCode.Double:
                data = new ArraySegment<byte>(BitConverter.GetBytes((double) value));
                break;
            case TypeCode.Single:
                data = new ArraySegment<byte>(BitConverter.GetBytes((float) value));
                break;
            case TypeCode.Object:
            case TypeCode.Decimal:
            default:
                typeCode = TypeCode.Object;
                
                var binaryObject = objectBinarySerializer.Serialize(value);
                data = new ArraySegment<byte>(binaryObject, 0, binaryObject.Length);

                break;
        }

        return new CacheItemForRequest(TypeCodeToFlag(typeCode), data);
    }

    public static DeserializationResult<T> Deserialize<T>(
        CacheItemResult item,
        IObjectBinarySerializer objectBinarySerializer)
    {
        var data = item.Data;
        if (data.IsEmpty)
        {
            return new DeserializationResult<T>
            {
                Result = default,
                IsEmpty = false
            };
        }

        var typeCode = (TypeCode) (item.Flags & TypeCodeDeserializationMask);
        if (typeCode == TypeCode.Empty)
        {
            return DeserializationResult<T>.Empty;
        }

        var type = typeof(T);

        if (Type.GetTypeCode(type) != TypeCode.Object
            || type == typeof(byte[]))
        {
            // deserialization for primitive types
            var value = DeserializePrimitive(item, typeCode);
            if (value == null)
            {
                return default;
            }

            return new DeserializationResult<T>
            {
                Result = (T) value,
                IsEmpty = false
            };
        }

        // deserialization for complex object types
        var deserializedValue = objectBinarySerializer.Deserialize<T>(item.Data.ToArray());

        return new DeserializationResult<T>
        {
            Result = deserializedValue,
            IsEmpty = false
        };
    }

    private static object DeserializePrimitive(CacheItemResult item, TypeCode typeCode)
    {
        if (item.Flags == RawDataFlag)
        {
            return item.Data.ToArray();
        }

        var data = item.Data;

        return typeCode switch
        {
            TypeCode.Empty => null,
            TypeCode.DBNull => null,
            TypeCode.String => DecodeData(data.Span),
            TypeCode.Boolean => BitConverter.ToBoolean(data.Span),
            TypeCode.Int16 => BitConverter.ToInt16(data.Span),
            TypeCode.Int32 => BitConverter.ToInt32(data.Span),
            TypeCode.Int64 => BitConverter.ToInt64(data.Span),
            TypeCode.UInt16 => BitConverter.ToUInt16(data.Span),
            TypeCode.UInt32 => BitConverter.ToUInt32(data.Span),
            TypeCode.UInt64 => BitConverter.ToUInt64(data.Span),
            TypeCode.Char => BitConverter.ToChar(data.Span),
            TypeCode.DateTime => DateTime.FromBinary(BitConverter.ToInt64(data.Span)),
            TypeCode.Double => BitConverter.ToDouble(data.Span),
            TypeCode.Single => BitConverter.ToSingle(data.Span),
            TypeCode.Byte => data.Span[0],
            TypeCode.SByte => (sbyte) data.Span[0],
            _ => throw new InvalidOperationException($"Unknown TypeCode was returned: {typeCode}")
        };
    }

    private static string DecodeData(ReadOnlySpan<byte> data)
    {
        if (data == null
            || data.Length == 0)
        {
            return null;
        }

        return Encoding.UTF8.GetString(data);
    }

    private static uint TypeCodeToFlag(TypeCode code)
    {
        return (uint) ((int) code | TypeCodeSerializationMask);
    }
}