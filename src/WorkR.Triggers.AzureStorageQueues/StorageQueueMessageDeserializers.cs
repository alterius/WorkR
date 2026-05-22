using System.Text.Json;
using Azure.Storage.Queues.Models;

namespace WorkR.Triggers.AzureStorageQueues
{
    public static class StorageQueueMessageDeserializers
    {
        public static StorageQueueMessageDeserializer<T> Json<T>(JsonSerializerOptions? options = null) => message =>
            Task.FromResult(
                JsonSerializer.Deserialize<T>(message.Body, options)
                    ?? throw new JsonException("Failed to deserialize json message body."));
    }
}
