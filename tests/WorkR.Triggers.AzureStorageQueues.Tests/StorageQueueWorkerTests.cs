using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace WorkR.Triggers.AzureStorageQueues.Tests;

/// <summary>
/// L2 tests: full IHost wired against a real Azurite queue. Each test gets its own uniquely named queue.
/// </summary>
[Trait("Category", "L2")]
public class StorageQueueWorkerTests : IClassFixture<AzuriteFixture>
{
    private readonly AzuriteFixture _azurite;

    public StorageQueueWorkerTests(AzuriteFixture azurite)
    {
        _azurite = azurite;
    }

    [Fact]
    public async Task WorkerIsInvoked_WhenMessageArrivesInQueue()
    {
        const string queueName = "worker-invoked";
        await CreateQueueWithMessageAsync(queueName, new Payload("hello"));
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging(b => b.ClearProviders());
                services.AddSingleton(received);
                services.AddStorageQueueWorker<Payload, CapturingWorker>(_ => _azurite.QueueServiceClient, queueName);
            })
            .Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        var name = await received.Task.WaitAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        name.ShouldBe("hello");
    }

    [Fact]
    public async Task EachMessageIsProcessedInItsOwnScope()
    {
        const string queueName = "scoped-messages";
        var queueClient = _azurite.CreateQueue(queueName);
        await queueClient.SendMessageAsync(JsonSerializer.Serialize(new Payload("a")), TestContext.Current.CancellationToken);
        await queueClient.SendMessageAsync(JsonSerializer.Serialize(new Payload("b")), TestContext.Current.CancellationToken);
        var scopeLog = new ScopeLog(expectedCount: 2);

        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging(b => b.ClearProviders());
                services.AddSingleton(scopeLog);
                services.AddScoped<ScopedId>();
                services.AddStorageQueueWorker<Payload, ScopeCapturingWorker>(_ => _azurite.QueueServiceClient, queueName);
            })
            .Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        await scopeLog.Done.WaitAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        scopeLog.CapturedIds.ShouldBeUnique();
        scopeLog.CapturedIds.Count.ShouldBe(2);
    }

    [Fact]
    public async Task WorkerCanDeleteMessage_AndMessageDoesNotReappear()
    {
        const string queueName = "delete-message";
        var queueClient = await CreateQueueWithMessageAsync(queueName, new Payload("delete-me"));
        var deleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging(b => b.ClearProviders());
                services.AddSingleton(deleted);
                services.AddStorageQueueWorker<Payload, DeletingWorker>(_ => _azurite.QueueServiceClient, queueName);
            })
            .Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        await deleted.Task.WaitAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        var remaining = await queueClient.ReceiveMessagesAsync(
            maxMessages: 32,
            cancellationToken: TestContext.Current.CancellationToken);
        remaining.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task CustomDeserializer_IsUsedWhenProvided()
    {
        const string queueName = "custom-deserializer";
        var queueClient = _azurite.CreateQueue(queueName);
        await queueClient.SendMessageAsync("raw-body", TestContext.Current.CancellationToken);
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging(b => b.ClearProviders());
                services.AddSingleton(received);
                services.AddStorageQueueWorker<string, StringCapturingWorker>(
                    _ => _azurite.QueueServiceClient,
                    queueName,
                    deserializerFactory: _ => msg => Task.FromResult(msg.Body.ToString().ToUpper()));
            })
            .Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        var value = await received.Task.WaitAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);

        value.ShouldBe("RAW-BODY");
    }

    private async Task<QueueClient> CreateQueueWithMessageAsync<T>(string name, T payload)
    {
        var client = _azurite.CreateQueue(name);
        await client.SendMessageAsync(JsonSerializer.Serialize(payload), TestContext.Current.CancellationToken);
        return client;
    }

    private record Payload(string Name);

    private sealed class CapturingWorker(TaskCompletionSource<string> received) : IWorker<StorageQueueTriggerContext<Payload>>
    {
        public Task Execute(StorageQueueTriggerContext<Payload> context, CancellationToken ct)
        {
            received.TrySetResult(context.Value.Name);
            return Task.CompletedTask;
        }
    }

    private sealed class DeletingWorker(TaskCompletionSource deleted) : IWorker<StorageQueueTriggerContext<Payload>>
    {
        public async Task Execute(StorageQueueTriggerContext<Payload> context, CancellationToken ct)
        {
            await context.DeleteMessageAsync(ct);
            deleted.TrySetResult();
        }
    }

    private sealed class StringCapturingWorker(TaskCompletionSource<string> received) : IWorker<StorageQueueTriggerContext<string>>
    {
        public Task Execute(StorageQueueTriggerContext<string> context, CancellationToken ct)
        {
            received.TrySetResult(context.Value);
            return Task.CompletedTask;
        }
    }

    private sealed class ScopedId
    {
        public Guid Value { get; } = Guid.NewGuid();
    }

    private sealed class ScopeLog(int expectedCount)
    {
        private readonly List<Guid> _ids = [];
        private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Done => _done.Task;
        public IReadOnlyList<Guid> CapturedIds { get { lock (_ids) return [.. _ids]; } }

        public void Add(Guid id)
        {
            lock (_ids)
            {
                _ids.Add(id);
                if (_ids.Count >= expectedCount) _done.TrySetResult();
            }
        }
    }

    private sealed class ScopeCapturingWorker(ScopedId scopedId, ScopeLog log) : IWorker<StorageQueueTriggerContext<Payload>>
    {
        public Task Execute(StorageQueueTriggerContext<Payload> context, CancellationToken ct)
        {
            log.Add(scopedId.Value);
            return Task.CompletedTask;
        }
    }
}
