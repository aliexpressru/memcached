using Aer.Memcached.Client.Interfaces;
using MessagePack;

namespace Aer.Memcached.Tests.Infrastructure;

internal class TestObjectBinarySerializer : IObjectBinarySerializer
{
	private int _serializationCount;
	private int _deserializationCount;

	public int SerializationsCount => _serializationCount;
	public int DeserializationsCount => _deserializationCount;
	
	public void ClearCounts()
	{
		_serializationCount = 0;
		_deserializationCount = 0;
	}

	public byte[] Serialize<T>(T value)
	{
		var data = MessagePackSerializer.Typeless.Serialize(value);

		Interlocked.Increment(ref _serializationCount);

		return data;
	}

	public T Deserialize<T>(byte[] serializedObject)
	{
		var deserializedObject = (T) MessagePackSerializer.Typeless.Deserialize(serializedObject);

		Interlocked.Increment(ref _deserializationCount);
		
		return deserializedObject;
	}
}
