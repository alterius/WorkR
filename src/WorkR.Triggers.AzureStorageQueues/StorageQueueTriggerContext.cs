using Azure.Storage.Queues.Models;

namespace WorkR.Triggers.AzureStorageQueues
{
    public sealed class StorageQueueTriggerContext<T> : ValueTriggerContext<T>
    {
        private readonly Func<CancellationToken, Task> _deleteMessage;

        public StorageQueueTriggerContext(
            Guid executionId,
            DateTimeOffset occurredAt,
            T value,
            QueueMessage queueMessage,
            Func<CancellationToken, Task> deleteMessage)
                : base(executionId, occurredAt, value)
        {
            ArgumentNullException.ThrowIfNull(queueMessage);
            ArgumentNullException.ThrowIfNull(deleteMessage);

            Message = queueMessage;
            _deleteMessage = deleteMessage;
        }

        public QueueMessage Message { get; }

        public Task DeleteMessageAsync(CancellationToken ct) => _deleteMessage(ct);
    }

    public sealed class StorageQueueTriggerContext : ValueTriggerContext<QueueMessage>
    {
        private readonly Func<CancellationToken, Task> _deleteMessage;

        public StorageQueueTriggerContext(
            Guid executionId,
            DateTimeOffset occurredAt,
            QueueMessage queueMessage,
            Func<CancellationToken, Task> deleteMessage)
                : base(executionId, occurredAt, queueMessage)
        {
            ArgumentNullException.ThrowIfNull(deleteMessage);

            _deleteMessage = deleteMessage;
        }

        public Task DeleteMessageAsync(CancellationToken ct) => _deleteMessage(ct);
    }
}
