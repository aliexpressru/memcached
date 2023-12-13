using Aer.Memcached.Client.Interfaces;

namespace Aer.Memcached.Client.Serializers;

public class PlainBinaryObjectBinarySerializer : IObjectBinarySerializer
{
	public byte[] Serialize<T>(T value)
	{
		throw new NotImplementedException();
	}

	public T Deserialize<T>(byte[] serializedObject)
	{
		throw new NotImplementedException();
	}
}
