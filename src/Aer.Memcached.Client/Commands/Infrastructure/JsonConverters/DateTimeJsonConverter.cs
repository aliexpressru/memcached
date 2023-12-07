using System.Globalization;
using Newtonsoft.Json;

namespace Aer.Memcached.Client.Commands.Infrastructure.JsonConverters;

/// <summary>
/// The <see cref="DateTime"/> converter for cases when this type is part of more complex object.
/// BSON spec stores dates to a millisecond precision. This converter ensures that precision is kept. 
/// </summary>
internal class DateTimeJsonConverter : JsonConverter<DateTime>
{
	public override void WriteJson(JsonWriter writer, DateTime value, JsonSerializer serializer)
	{
		var dateTimeString = value.ToString("O");
		writer.WriteValue(dateTimeString);
	}

	public override DateTime ReadJson(
		JsonReader reader,
		Type objectType,
		DateTime existingValue,
		bool hasExistingValue,
		JsonSerializer serializer)
	{
		if (reader.TokenType == JsonToken.String)
		{
			var parsedDateTime = DateTime.Parse((string) reader.Value, styles: DateTimeStyles.RoundtripKind);

			return parsedDateTime;
		}

		throw new JsonSerializationException(
			$"Can't deserialize value as {typeof(DateTime)}. Expected token type {nameof(JsonToken.String)} but found {reader.TokenType}");
	}
}
