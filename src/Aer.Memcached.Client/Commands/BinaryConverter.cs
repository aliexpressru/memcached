using System.Collections;
using System.Reflection;
using System.Text;
using Aer.Memcached.Client.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Aer.Memcached.Client.Commands;

public static class BinaryConverter
{
    private const uint RawDataFlag = 0xfa52;
    private const uint TypeCodeSerializationMask = 0x0100;
    private const uint TypeCodeDeserializationMask = 0xff;
    
    private static readonly JsonSerializer DefaultSerializer = JsonSerializer.Create(new JsonSerializerSettings
    {
        Converters = new List<JsonConverter>(new[] { new StringEnumConverter() }),
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy()
        }
    });

    public static JsonSerializer Serializer = DefaultSerializer;
    
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
        
        var part1 = (uint)((offsetSpan[0] << 24) | (offsetSpan[1] << 16) | (offsetSpan[2] << 8) | offsetSpan[3]);
        var part2 = (uint)((offsetSpan[4] << 24) | (offsetSpan[5] << 16) | (offsetSpan[6] << 8) | offsetSpan[7]);
        
        return ((ulong)part1 << 32) | part2;
    }

    public static void EncodeUInt32(uint value, Span<byte> span, int offset)
    {
        var offsetSpan = span[offset..];
        
        offsetSpan[0] = (byte)(value >> 24);
        offsetSpan[1] = (byte)(value >> 16);
        offsetSpan[2] = (byte)(value >> 8);
        offsetSpan[3] = (byte)(value & 255);
    }
    
    public static void EncodeUInt64(ulong value, Span<byte> span, int offset)
    {
        var offsetSpan = span[offset..];

        offsetSpan[0] = (byte)(value >> 56);
        offsetSpan[1] = (byte)(value >> 48);
        offsetSpan[2] = (byte)(value >> 40);
        offsetSpan[3] = (byte)(value >> 32);
        offsetSpan[4] = (byte)(value >> 24);
        offsetSpan[5] = (byte)(value >> 16);
        offsetSpan[6] = (byte)(value >> 8);
        offsetSpan[7] = (byte)(value & 255);
    }

    public static byte[] Encode(string value)
    {
        return string.IsNullOrEmpty(value) 
            ? null 
            : Encoding.UTF8.GetBytes(value);
    }

    public static CacheItemForRequest Serialize(object value)
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
        
        var typeCode = Type.GetTypeCode( value.GetType() );
        ArraySegment<byte> data;
        
        switch (typeCode)
        {
            case TypeCode.Empty: data = new ArraySegment<byte>(Array.Empty<byte>()); break;
            case TypeCode.DBNull: data = new ArraySegment<byte>(Array.Empty<byte>()); break;
            case TypeCode.String: data = new ArraySegment<byte>(Encode(value.ToString()) ?? Array.Empty<byte>()); break;
            case TypeCode.Boolean: data = new ArraySegment<byte>(BitConverter.GetBytes((bool)value)); break;
            case TypeCode.SByte: data = new ArraySegment<byte>(BitConverter.GetBytes((sbyte)value)); break;
            case TypeCode.Byte: data = new ArraySegment<byte>(BitConverter.GetBytes((byte)value)); break;
            case TypeCode.Int16: data = new ArraySegment<byte>(BitConverter.GetBytes((short)value)); break;
            case TypeCode.Int32: data = new ArraySegment<byte>(BitConverter.GetBytes((int)value)); break;
            case TypeCode.Int64: data = new ArraySegment<byte>(BitConverter.GetBytes((long)value)); break;
            case TypeCode.UInt16: data = new ArraySegment<byte>(BitConverter.GetBytes((ushort)value)); break;
            case TypeCode.UInt32: data = new ArraySegment<byte>(BitConverter.GetBytes((uint)value)); break;
            case TypeCode.UInt64: data = new ArraySegment<byte>(BitConverter.GetBytes((ulong)value)); break;
            case TypeCode.Char: data = new ArraySegment<byte>(BitConverter.GetBytes((char)value)); break;
            case TypeCode.DateTime: data = new ArraySegment<byte>(BitConverter.GetBytes(((DateTime)value).ToBinary())); break;
            case TypeCode.Double: data = new ArraySegment<byte>(BitConverter.GetBytes((double)value)); break;
            case TypeCode.Single: data = new ArraySegment<byte>(BitConverter.GetBytes((float)value)); break;
            default:
                typeCode = TypeCode.Object;
                using (var ms = new MemoryStream())
                {
                    using (var writer = new BsonDataWriter(ms))
                    {
                        Serializer.Serialize(writer, value);
                        data = new ArraySegment<byte>(ms.ToArray(), 0, (int)ms.Length);
                    }
                }
                break;
        }

        return new CacheItemForRequest(TypeCodeToFlag(typeCode), data);
    }

    public static T Deserialize<T>(CacheItemResult item)
    {
        var data = item.Data;
        if (data.IsEmpty)
        {
            return default;
        }
        var typeCode = (TypeCode)(item.Flags & TypeCodeDeserializationMask);
        if (typeCode == TypeCode.Empty)
        {
            return default;
        }
    
        var type = typeof(T);
        if (Type.GetTypeCode(type) != TypeCode.Object || type == typeof(byte[]))
        {
            // deserialization for primitive types
            var value = Deserialize(item, typeCode);
            if (value == null)
            {
                return default;
            }

            return (T)value;
        }

        using var ms = new MemoryStream(item.Data.ToArray());
        using var reader = new BsonDataReader(ms);
        if (typeof(T).GetTypeInfo().ImplementedInterfaces.Contains(typeof(IEnumerable)))
        {
            reader.ReadRootValueAsArray = true;
        }
                
        return Serializer.Deserialize<T>(reader);
    }
    
    private static object Deserialize(CacheItemResult item, TypeCode typeCode)
    {
        if (item.Flags == RawDataFlag)
        {
            return item.Data.ToArray();
        }
        
        var data = item.Data;
    
        switch (typeCode)
        {
            case TypeCode.Empty: return null;
            case TypeCode.DBNull: return null;
            case TypeCode.String: return DecodeData(data.Span);
            case TypeCode.Boolean: return BitConverter.ToBoolean(data.Span);
            case TypeCode.Int16: return BitConverter.ToInt16(data.Span);
            case TypeCode.Int32: return BitConverter.ToInt32(data.Span);
            case TypeCode.Int64: return BitConverter.ToInt64(data.Span);
            case TypeCode.UInt16: return BitConverter.ToUInt16(data.Span);
            case TypeCode.UInt32: return BitConverter.ToUInt32(data.Span);
            case TypeCode.UInt64: return BitConverter.ToUInt64(data.Span);
            case TypeCode.Char: return BitConverter.ToChar(data.Span);
            case TypeCode.DateTime: return DateTime.FromBinary(BitConverter.ToInt64(data.Span));
            case TypeCode.Double: return BitConverter.ToDouble(data.Span);
            case TypeCode.Single: return BitConverter.ToSingle(data.Span);
            case TypeCode.Byte: return data.Span[0];
            case TypeCode.SByte: return (sbyte)data.Span[0];
            default: throw new InvalidOperationException("Unknown TypeCode was returned: " + typeCode);
        }
    }

    private static string DecodeData(ReadOnlySpan<byte> data)
    {
        if (data == null || data.Length == 0)
        {
            return null;
        }

        return Encoding.UTF8.GetString(data);
    }
    
    private static uint TypeCodeToFlag(TypeCode code)
    {
        return (uint)((int)code | TypeCodeSerializationMask);
    }
}