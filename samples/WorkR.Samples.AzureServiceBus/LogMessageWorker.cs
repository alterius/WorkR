using Microsoft.Extensions.Logging;
using WorkR.Triggers.AzureServiceBus;

namespace WorkR.Samples.AzureServiceBus
{
    public class LogMessageWorker<T> : IWorker<ServiceBusTriggerContext<T>>
    {
        private readonly ILogger _logger;

        public LogMessageWorker(ILogger<LogMessageWorker<T>> logger)
        {
            _logger = logger;
        }

        public Task ExecuteAsync(ServiceBusTriggerContext<T> source, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Message with id {messageId} is {message}", source.Args.Message.MessageId, source.Value);
            return Task.CompletedTask;
        }
    }
}
