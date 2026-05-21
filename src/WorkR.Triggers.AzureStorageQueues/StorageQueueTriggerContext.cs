using Azure.Storage.Queues.Models;

namespace WorkR.Triggers.AzureStorageQueues
{
    public sealed class StorageQueueTriggerContext<T> : ValueTriggerContext<T>
    {
        private readonly Func<CancellationToken, Task> _deleteMessage;
        private readonly Func<CancellationToken, Task> _deadLetterMessage;

        public StorageQueueTriggerContext(
            Guid executionId,
            DateTimeOffset occurredAt,
            T value,
            QueueMessage queueMessage,
            Func<CancellationToken, Task> deleteMessage,
            Func<CancellationToken, Task> deadLetterMessage)
                : base(executionId, occurredAt, value)
        {
            ArgumentNullException.ThrowIfNull(queueMessage);
            ArgumentNullException.ThrowIfNull(deleteMessage);
            ArgumentNullException.ThrowIfNull(deadLetterMessage);

            Message = queueMessage;
            _deleteMessage = deleteMessage;
            _deadLetterMessage = deadLetterMessage;
        }

        public QueueMessage Message { get; }

        public Task DeleteMessageAsync(CancellationToken ct) => _deleteMessage(ct);
        public Task DeadLetterMessageAsync(CancellationToken ct) => _deadLetterMessage(ct);
    }

    public sealed class StorageQueueTriggerContext : ValueTriggerContext<QueueMessage>
    {
        private readonly Func<CancellationToken, Task> _deleteMessage;
        private readonly Func<CancellationToken, Task> _deadLetterMessage;

        public StorageQueueTriggerContext(
            Guid executionId,
            DateTimeOffset occurredAt,
            QueueMessage queueMessage,
            Func<CancellationToken, Task> deleteMessage,
            Func<CancellationToken, Task> deadLetterMessage)
                : base(executionId, occurredAt, queueMessage)
        {
            ArgumentNullException.ThrowIfNull(deleteMessage);
            ArgumentNullException.ThrowIfNull(deadLetterMessage);

            _deleteMessage = deleteMessage;
            _deadLetterMessage = deadLetterMessage;
        }

        public Task DeleteMessageAsync(CancellationToken ct) => _deleteMessage(ct);
        public Task DeadLetterMessageAsync(CancellationToken ct) => _deadLetterMessage(ct);
    }
}
