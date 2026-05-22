using Azure.Storage.Queues.Models;

namespace WorkR.Triggers.AzureStorageQueues
{
    public delegate Task<T> StorageQueueMessageDeserializer<T>(QueueMessage message);
}
