namespace Aer.Memcached.Client.Interfaces;

/// <summary>
/// Common interface for binary serializers used to binary serialize
/// non-primitive typed objects before storing them in memcached.
/// </summary>
public interface IObjectBinarySerializer
{
	public byte[] Serialize<T>(T value);
	
	public T Deserialize<T>(byte[] serializedObject);
}
