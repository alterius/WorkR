using Azure.Storage.Queues.Models;

namespace WorkR.Triggers.AzureStorageQueues
{
    public interface IStorageQueueMessageDeserializer<T>
    {
        Task<T> Deserialize(QueueMessage queueMessage);
    }
}
