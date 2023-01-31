using Newtonsoft.Json;

namespace Aer.Memcached;

public static class BinaryConverterConfigurator
{
    public static void SetSerializer(JsonSerializer serializer)
    {
        Client.Commands.BinaryConverter.Serializer = serializer;
    }
}