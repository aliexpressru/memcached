using Aer.Memcached.Client.Commands.Infrastructure.JsonConverters;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Aer.Memcached.Client.Serializers;

internal static class DefaultJsonSerializer
{
	private static readonly JsonSerializerSettings _serializerSettings = new()
	{
		Converters = new List<JsonConverter>()
		{
			new StringEnumConverter(),
			new DateTimeJsonConverter(),
			new DateTimeOffsetJsonConverter()
		},
		NullValueHandling = NullValueHandling.Ignore,
		ContractResolver = new DefaultContractResolver
		{
			NamingStrategy = new SnakeCaseNamingStrategy()
		},
		ReferenceLoopHandling = ReferenceLoopHandling.Ignore
	};

	private static JsonSerializer _serializerInstance = JsonSerializer.Create(_serializerSettings);

	public static JsonSerializer Instance => _serializerInstance;

}
