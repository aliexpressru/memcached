using Aer.Memcached.Client.Interfaces;

namespace Aer.Memcached.Client.Serializers;

public class PlainBinaryObjectBinarySerializer : IObjectBinarySerializer
{
	public void Serialize<T>(T value, Stream stream)
	{
		throw new NotImplementedException();
	}

	public T Deserialize<T>(Stream stream)
	{
		throw new NotImplementedException();
	}
}
