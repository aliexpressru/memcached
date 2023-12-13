namespace Aer.Memcached.Client.Interfaces;

/// <summary>
/// Common interface for binary serializers used to binary serialize
/// non-primitive typed objects before storing them in memcached.
/// </summary>
public interface IObjectBinarySerializer
{
	public void Serialize<T>(T value, Stream stream);
	
	public T Deserialize<T>(Stream stream);
}
