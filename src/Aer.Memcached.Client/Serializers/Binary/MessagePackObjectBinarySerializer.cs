using Aer.Memcached.Client.Interfaces;
using MessagePack;

namespace Aer.Memcached.Client.Serializers;

internal class MessagePackObjectBinarySerializer : IObjectBinarySerializer
{
	public byte[] Serialize<T>(T value)
	{
		var data = MessagePackSerializer.Typeless.Serialize(value);

		return data;
	}

	public T Deserialize<T>(byte[] serializedObject)
	{
		var deserializedObject = (T)MessagePackSerializer.Typeless.Deserialize(serializedObject);
		
		return deserializedObject;
	}
}
