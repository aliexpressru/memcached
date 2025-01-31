using System.Runtime.CompilerServices;
using System.Text;
using Aer.Memcached.Client.Commands.Infrastructure;
using Aer.Memcached.Client.Interfaces;
using Aer.Memcached.Client.Models;

namespace Aer.Memcached.Client.Serializers;

/// <summary>
/// Class for binary serializing objects before storing them to memcached.   
/// </summary>
public class BinarySerializer
{
    private const uint RawDataFlag = 0xfa52;
    private const uint TypeCodeSerializationMask = 0x0100;
    private const uint TypeCodeDeserializationMask = 0xff;
    
    private readonly IObjectBinarySerializer _objectBinarySerializer;

    public BinarySerializer(IObjectBinarySerializerFactory objectBinarySerializerFactory)
    {
        _objectBinarySerializer = objectBinarySerializerFactory.Create();
    }

    internal CacheItemForRequest Serialize(object value)
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
                data = new ArraySegment<byte>(BinaryConverter.Encode(value.ToString()) ?? Array.Empty<byte>());
                break;
            case TypeCode.Boolean:
                data = new ArraySegment<byte>(BitConverter.GetBytes((bool) value));
                break;
            case TypeCode.SByte:
                data = new ArraySegment<byte>(GetBytes((sbyte) value));
                break;
            case TypeCode.Byte:
                data = new ArraySegment<byte>([(byte)value]);
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
                
                var serializedObject = _objectBinarySerializer.Serialize(value);
                data = new ArraySegment<byte>(serializedObject, 0, serializedObject.Length);

                break;
        }

        return new CacheItemForRequest(TypeCodeToFlag(typeCode), data);
    }

    internal DeserializationResult<T> Deserialize<T>(
        CacheItemResult item)
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
            // the flags value is set during item serialization
            // it stores the cache item System.Runtime.TypeCode
            // if it is not set -> Flags == 0 or Flags contain some unknown value 
            // then the item is considered empty
            // The Data property for such empty items though
            // will contain ASCII bytes for "Not found" string
            // 0x4E, 0x6F, 0x74, 0x20, 0x66, 0x6F, 0x75, 0x6E, 0x64
            // we don't check them since typeCode absence already tells us that the item is empty
            // it does, though impede using this library for reading existing
            // memcached keys that were written not using this library
            // since they may set Flags to some arbitrary value which we interpret 
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
        var deserializedValue = _objectBinarySerializer.Deserialize<T>(item.Data.ToArray());

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

    private static byte[] GetBytes(short value)
    {
        return BitConverter.GetBytes(value);
    }
}
