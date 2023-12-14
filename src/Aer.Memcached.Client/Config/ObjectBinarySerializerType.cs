using Aer.Memcached.Client.Interfaces;

namespace Aer.Memcached.Client.Config;

/// <summary>
/// Determines the type of the binary serializer to use for serializing values of non-primitive types.
/// </summary>
public enum ObjectBinarySerializerType
{
	/// <summary>
	/// BSON binary serializer. Can't handle dicitonaries with non-primitive keys.
	/// </summary>
	Bson,
	
	/// <summary>
	/// MessagePack binary serializer. Can't handle reference loops.
	/// </summary>
	MessagePack,
	
	/// <summary>
	/// Custom serializer. The <see cref="IObjectBinarySerializer"/> implementation
	/// must be registerred in DI container.
	/// </summary>
	Custom
}
