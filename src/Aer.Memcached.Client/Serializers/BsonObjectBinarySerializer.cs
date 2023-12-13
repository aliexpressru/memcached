using System.Collections;
using System.Reflection;
using Aer.Memcached.Client.Interfaces;
using Newtonsoft.Json.Bson;

namespace Aer.Memcached.Client.Serializers;

internal class BsonObjectBinarySerializer : IObjectBinarySerializer
{
	public void Serialize<T>(T value, Stream stream)
	{
		var writer = new BsonDataWriter(stream);
		
		NewtonsoftJsonSerializer.Default.Serialize(writer, value);
		
		writer.Flush();
	}

	public T Deserialize<T>(Stream stream)
	{
		var reader = new BsonDataReader(stream);

		if (typeof(T).GetTypeInfo().ImplementedInterfaces.Contains(typeof(IEnumerable))
			// Dictionary<TKey,TValue> implements IEnumerable but we should read it as an object, not an array
			&& !typeof(T).GetTypeInfo().ImplementedInterfaces.Contains(typeof(IDictionary)))
		{
			reader.ReadRootValueAsArray = true;
		}

		var deserializedValue = NewtonsoftJsonSerializer.Default.Deserialize<T>(reader);

		return deserializedValue;
	}
}
