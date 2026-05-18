using System.Text.Json;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
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
        var queueClient = await CreateQueueWithMessageAsync("worker-invoked", new Payload("hello"));
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging(b => b.ClearProviders());
                services.AddSingleton(received);
                services.AddStorageQueueTrigger<Payload, CapturingWorker>(_ => queueClient);
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
        var queueClient = _azurite.CreateQueue("scoped-messages");
        await queueClient.SendMessageAsync(JsonSerializer.Serialize(new Payload("a")), TestContext.Current.CancellationToken);
        await queueClient.SendMessageAsync(JsonSerializer.Serialize(new Payload("b")), TestContext.Current.CancellationToken);
        var scopeLog = new ScopeLog(expectedCount: 2);

        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging(b => b.ClearProviders());
                services.AddSingleton(scopeLog);
                services.AddScoped<ScopedId>();
                services.AddStorageQueueTrigger<Payload, ScopeCapturingWorker>(_ => queueClient);
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
        var queueClient = await CreateQueueWithMessageAsync("delete-message", new Payload("delete-me"));
        var deleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging(b => b.ClearProviders());
                services.AddSingleton(deleted);
                services.AddStorageQueueTrigger<Payload, DeletingWorker>(_ => queueClient);
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
        var queueClient = _azurite.CreateQueue("custom-deserializer");
        await queueClient.SendMessageAsync("raw-body", TestContext.Current.CancellationToken);
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging(b => b.ClearProviders());
                services.AddSingleton(received);
                var deserializer = Substitute.For<IStorageQueueMessageDeserializer<string>>();
                deserializer.Deserialize(Arg.Any<QueueMessage>())
                    .Returns(ci => Task.FromResult(ci.Arg<QueueMessage>().Body.ToString().ToUpper()));
                services.AddStorageQueueTrigger<string, StringCapturingWorker>(
                    _ => queueClient,
                    deserializerFactory: _ => deserializer);
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
