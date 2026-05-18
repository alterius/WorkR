using Azure.Storage.Queues;
using Testcontainers.Azurite;

namespace WorkR.Triggers.AzureStorageQueues.Tests;

public sealed class AzuriteFixture : IAsyncLifetime
{
    private readonly AzuriteContainer _container = new AzuriteBuilder().Build();

    public string ConnectionString => _container.GetConnectionString();

    public QueueClient CreateQueue(string name)
    {
        var options = new QueueClientOptions(QueueClientOptions.ServiceVersion.V2023_11_03);
        var client = new QueueClient(ConnectionString, name, options);
        client.CreateIfNotExists();
        return client;
    }

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
