using Azure.Storage.Queues.Models;

namespace WorkR.Triggers.AzureStorageQueues
{
    public class StorageQueueTriggerContext<T> : ValueTriggerContext<T>
    {
        private readonly Func<CancellationToken, Task> _deleteMessage;

        public StorageQueueTriggerContext(
            DateTimeOffset occurredAt,
            T value,
            QueueMessage queueMessage,
            Func<CancellationToken, Task> deleteMessage)
                : base(occurredAt, value)
        {
            ArgumentNullException.ThrowIfNull(queueMessage);
            ArgumentNullException.ThrowIfNull(deleteMessage);

            Message = queueMessage;
            _deleteMessage = deleteMessage;
        }

        public QueueMessage Message { get; }

        public Task DeleteMessageAsync(CancellationToken ct) => _deleteMessage(ct);
    }

    public class StorageQueueTriggerContext : ValueTriggerContext<QueueMessage>
    {
        private readonly Func<CancellationToken, Task> _deleteMessage;

        public StorageQueueTriggerContext(
            DateTimeOffset occurredAt,
            QueueMessage queueMessage,
            Func<CancellationToken, Task> deleteMessage)
                : base(occurredAt, queueMessage)
        {
            ArgumentNullException.ThrowIfNull(deleteMessage);

            _deleteMessage = deleteMessage;
        }

        public Task DeleteMessageAsync(CancellationToken ct) => _deleteMessage(ct);
    }
}
