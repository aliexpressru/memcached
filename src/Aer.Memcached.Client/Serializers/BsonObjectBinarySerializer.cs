using System.Collections;
using System.Reflection;
using Aer.Memcached.Client.Interfaces;
using Newtonsoft.Json.Bson;

namespace Aer.Memcached.Client.Serializers;

internal class BsonObjectBinarySerializer : IObjectBinarySerializer
{
	public byte[] Serialize<T>(T value)
	{
		using var ms = new MemoryStream();
		using var writer = new BsonDataWriter(ms);

		DefaultJsonSerializer.Instance.Serialize(writer, value);

		return ms.ToArray();
	}

	public T Deserialize<T>(byte[] serializedObject)
	{
		using var ms = new MemoryStream(serializedObject);
		using var reader = new BsonDataReader(ms);

		if (typeof(T).GetTypeInfo().ImplementedInterfaces.Contains(typeof(IEnumerable))
			// Dictionary<TKey,TValue> implements IEnumerable but we should read it as an object, not an array
			&& !typeof(T).GetTypeInfo().ImplementedInterfaces.Contains(typeof(IDictionary)))
		{
			reader.ReadRootValueAsArray = true;
		}

		var deserializedValue = DefaultJsonSerializer.Instance.Deserialize<T>(reader);

		return deserializedValue;
	}
}
