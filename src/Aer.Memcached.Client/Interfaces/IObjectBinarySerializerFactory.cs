namespace Aer.Memcached.Client.Interfaces;

/// <summary>
/// Interface for resolving binary object serializer.
/// </summary>
public interface IObjectBinarySerializerFactory
{
	public IObjectBinarySerializer Create();
}
