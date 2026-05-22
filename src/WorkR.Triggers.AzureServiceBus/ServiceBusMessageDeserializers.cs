using System.Text.Json;

namespace WorkR.Triggers.AzureServiceBus
{
    public static class ServiceBusMessageDeserializers
    {
        public static ServiceBusMessageDeserializer<T> Json<T>(JsonSerializerOptions? options = null) => args =>
            Task.FromResult(
                JsonSerializer.Deserialize<T>(args.Message.Body, options)
                    ?? throw new JsonException("Failed to deserialize json message body."));
    }
}
