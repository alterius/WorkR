using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace WorkR.Samples.AzureServiceBus
{
    public sealed class SendTestMessageWorker : IWorker<EmptyTriggerContext>
    {
        private readonly ServiceBusSender _sender;

        public SendTestMessageWorker(ServiceBusSender sender)
        {
            _sender = sender;
        }

        public async Task ExecuteAsync(EmptyTriggerContext source, CancellationToken cancellationToken)
        {
            var message = new TestMessage
            {
                ExecutionId = source.ExecutionId,
                OccurredAt = source.OccurredAt,
                Value = "Hello world!"
            };

            var serviceBusMessage = new ServiceBusMessage(
                JsonSerializer.SerializeToUtf8Bytes(message));

            await _sender.SendMessageAsync(serviceBusMessage, cancellationToken);
        }
    }
}
