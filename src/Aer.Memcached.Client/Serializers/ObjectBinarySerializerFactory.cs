using Aer.Memcached.Client.Config;
using Aer.Memcached.Client.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Aer.Memcached.Client.Serializers;

public class ObjectBinarySerializerFactory : IObjectBinarySerializerFactory
{
	private readonly IServiceProvider _serviceProvider;
	private readonly MemcachedConfiguration _memcachedConfiguration;

	public ObjectBinarySerializerFactory(IOptions<MemcachedConfiguration> memcachedConfiguration, IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider;
		_memcachedConfiguration = memcachedConfiguration.Value;
	}

	public IObjectBinarySerializer Create() =>
		_memcachedConfiguration.BinarySerializerType switch
		{
			ObjectBinarySerializerType.Bson => new BsonObjectBinarySerializer(),
			ObjectBinarySerializerType.PlainBinary => new PlainBinaryObjectBinarySerializer(),
			ObjectBinarySerializerType.Custom => _serviceProvider.GetRequiredService<IObjectBinarySerializer>(),
			_ => throw new ArgumentOutOfRangeException($"Can't create serializer of type {_memcachedConfiguration.BinarySerializerType}")
		};
}
