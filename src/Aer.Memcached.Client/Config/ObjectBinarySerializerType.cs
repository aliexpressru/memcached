using Aer.Memcached.Client.Interfaces;

namespace Aer.Memcached.Client.Config;

public enum ObjectBinarySerializerType
{
	/// <summary>
	/// BSON binary serializer.
	/// </summary>
	Bson,
	
	/// <summary>
	/// Plain binary serializer.
	/// </summary>
	PlainBinary,
	
	/// <summary>
	/// Custom serializer. The <see cref="IObjectBinarySerializer"/> must be registerred.
	/// </summary>
	Custom
}
