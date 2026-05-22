using Azure.Storage.Queues;
using Testcontainers.Azurite;

namespace WorkR.Triggers.AzureStorageQueues.Tests;

public sealed class AzuriteFixture : IAsyncLifetime
{
    private readonly AzuriteContainer _container = new AzuriteBuilder().Build();

    public string ConnectionString => _container.GetConnectionString();

    public QueueServiceClient QueueServiceClient =>
        new(ConnectionString, new QueueClientOptions(QueueClientOptions.ServiceVersion.V2023_11_03));

    public QueueClient CreateQueue(string name)
    {
        var client = QueueServiceClient.GetQueueClient(name);
        client.CreateIfNotExists();
        return client;
    }

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
