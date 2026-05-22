using Microsoft.Extensions.Logging;
using WorkR.Triggers.AzureStorageQueues;

namespace WorkR.Samples.AzureStorageQueue
{
    public class LogMessageWorker<T> : IWorker<StorageQueueTriggerContext<T>>
    {
        private readonly ILogger _logger;

        public LogMessageWorker(ILogger<LogMessageWorker<T>> logger)
        {
            _logger = logger;
        }

        public Task ExecuteAsync(StorageQueueTriggerContext<T> source, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Message with id {messageId} is {message}", source.Message.MessageId, source.Value);
            return Task.CompletedTask;
        }
    }
}
