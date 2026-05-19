using Azure.Messaging.ServiceBus;

namespace WorkR.Triggers.AzureServiceBus
{
    public delegate Task<T> ServiceBusMessageDeserializer<T>(ProcessMessageEventArgs args);
}
