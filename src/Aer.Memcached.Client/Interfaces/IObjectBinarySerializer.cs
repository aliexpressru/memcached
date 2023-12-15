namespace Aer.Memcached.Client.Interfaces;

/// <summary>
/// Common interface for binary serializers used to serialize
/// objects of non-primitive types before storing them in memcached.
/// </summary>
public interface IObjectBinarySerializer
{
	public byte[] Serialize<T>(T value);
	
	public T Deserialize<T>(byte[] serializedObject);
}
