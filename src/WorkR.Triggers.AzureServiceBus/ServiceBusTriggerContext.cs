using Azure.Messaging.ServiceBus;

namespace WorkR.Triggers.AzureServiceBus
{
    public sealed class ServiceBusTriggerContext : ValueTriggerContext<ProcessMessageEventArgs>
    {
        public ServiceBusTriggerContext(
            Guid executionId,
            DateTimeOffset occurredAt,
            ProcessMessageEventArgs value)
                : base(executionId, occurredAt, value)
        {
        }
    }

    public sealed class ServiceBusTriggerContext<T> : ValueTriggerContext<T>
    {
        public ServiceBusTriggerContext(
            Guid executionId,
            DateTimeOffset occurredAt,
            T value,
            ProcessMessageEventArgs args)
                : base(executionId, occurredAt, value)
        {
            ArgumentNullException.ThrowIfNull(args);

            Args = args;
        }

        public ProcessMessageEventArgs Args { get; }
    }
}
