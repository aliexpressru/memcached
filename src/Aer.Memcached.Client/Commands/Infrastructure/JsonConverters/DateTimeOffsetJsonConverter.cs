using System.Globalization;
using Newtonsoft.Json;

namespace Aer.Memcached.Client.Commands.Infrastructure.JsonConverters;

/// <summary>
/// The <see cref="DateTimeOffset"/> converter for cases when we want to store this object either by itself or as part of more complex object.
/// Json serializer serializes this type as value and BSON serializer fails to serilize it since it's not an object.
/// </summary>
internal class DateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
	private const string DateTimeOffsetStringPropertyName = nameof(DateTimeOffsetStringPropertyName);

	public override void WriteJson(JsonWriter writer, DateTimeOffset value, JsonSerializer serializer)
	{
		var dateTimeOffsetString = value.ToString("O");
		
		writer.WriteStartObject();
		{
			writer.WritePropertyName(DateTimeOffsetStringPropertyName);
			writer.WriteValue(dateTimeOffsetString);
		}
		
		writer.WriteEndObject();
	}

	public override DateTimeOffset ReadJson(
		JsonReader reader,
		Type objectType,
		DateTimeOffset existingValue,
		bool hasExistingValue,
		JsonSerializer serializer)
	{
		if (reader.TokenType != JsonToken.StartObject)
		{
			throw new JsonSerializationException(
				$"Can't deserialize value as {typeof(DateTimeOffset)}. Expected token type {nameof(JsonToken.StartObject)} but found {reader.TokenType}");
		}

		// read into object
		reader.Read();
		
		// check property name
		if (reader.TokenType != JsonToken.PropertyName || (string)reader.Value != DateTimeOffsetStringPropertyName)
		{
			throw new JsonSerializationException(
				$"Can't deserialize value as {typeof(DateTimeOffset)}. Expected json property with name {DateTimeOffsetStringPropertyName} but found {reader.TokenType} with value {(string)reader.Value}");
		}

		// read first property
		reader.Read();

		// get first propertyValue 
		var dateTimeOffsetString = (string) reader.Value;

		var parsedDateTime = DateTimeOffset.Parse(dateTimeOffsetString, styles: DateTimeStyles.RoundtripKind);

		// read out of object to not leave reader in incorrect state
		reader.Read();

		return parsedDateTime;
	}
}
