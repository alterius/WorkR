using System.Text.Json;
using Azure.Storage.Queues;

namespace WorkR.Samples.AzureStorageQueue
{
    public class SendTestMessageWorker : IWorker<EmptyTriggerContext>
    {
        private readonly QueueClient _queueClient;

        public SendTestMessageWorker(QueueClient queueClient)
        {
            _queueClient = queueClient;
        }

        public async Task ExecuteAsync(EmptyTriggerContext source, CancellationToken cancellationToken)
        {
            var message = new TestMessage
            {
                ExecutionId = source.ExecutionId,
                OccurredAt = source.OccurredAt,
                Value = "Hello world!"
            };

            await _queueClient.SendMessageAsync(
                JsonSerializer.Serialize(message),
                cancellationToken);
        }
    }
}
