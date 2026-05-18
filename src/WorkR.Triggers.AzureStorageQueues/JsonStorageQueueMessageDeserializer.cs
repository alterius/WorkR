using System.Text.Json;
using Azure.Storage.Queues.Models;

namespace WorkR.Triggers.AzureStorageQueues
{
    public sealed class JsonStorageQueueMessageDeserializer<T> : IStorageQueueMessageDeserializer<T>
    {
        private readonly JsonSerializerOptions? _options;

        public JsonStorageQueueMessageDeserializer(JsonSerializerOptions? serializerOptions = null)
        {
            _options = serializerOptions;
        }

        public Task<T> Deserialize(QueueMessage queueMessage) =>
            Task.FromResult(
                JsonSerializer.Deserialize<T>(queueMessage.Body, _options)
                    ?? throw new InvalidOperationException("Failed to deserialize json message body."));
    }
}
