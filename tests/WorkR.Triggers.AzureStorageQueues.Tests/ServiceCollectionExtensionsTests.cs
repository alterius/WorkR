using Azure.Storage.Queues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;

namespace WorkR.Triggers.AzureStorageQueues.Tests;

[Trait("Category", "L0")]
public class ServiceCollectionExtensionsTests
{
    private const string QueueName = "test-queue";

    [Fact]
    public void AddStorageQueueTrigger_WithBuilder_RegistersHostedService()
    {
        var services = new ServiceCollection();

        services.AddStorageQueueTrigger(
            _ => Substitute.For<QueueServiceClient>(),
            QueueName,
            builder => builder.AddWorker<FakeWorker>());

        services.ShouldContain(d => d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddStorageQueueTrigger_Generic_RegistersHostedService()
    {
        var services = new ServiceCollection();

        services.AddStorageQueueTrigger<FakeWorker>(_ => Substitute.For<QueueServiceClient>(), QueueName);

        services.ShouldContain(d => d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddStorageQueueTrigger_Typed_WithBuilder_RegistersHostedService()
    {
        var services = new ServiceCollection();

        services.AddStorageQueueTrigger<string>(
            _ => Substitute.For<QueueServiceClient>(),
            QueueName,
            builder => builder.AddWorker<FakeTypedWorker>());

        services.ShouldContain(d => d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddStorageQueueTrigger_TypedGeneric_RegistersHostedService()
    {
        var services = new ServiceCollection();

        services.AddStorageQueueTrigger<string, FakeTypedWorker>(_ => Substitute.For<QueueServiceClient>(), QueueName);

        services.ShouldContain(d => d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddStorageQueueTrigger_RegistersSystemTimeProviderWhenNoneRegistered()
    {
        var services = new ServiceCollection();

        services.AddStorageQueueTrigger<FakeWorker>(_ => Substitute.For<QueueServiceClient>(), QueueName);

        services.ShouldContain(d => d.ServiceType == typeof(TimeProvider));
    }

    [Fact]
    public void AddStorageQueueTrigger_DoesNotReplaceAlreadyRegisteredTimeProvider()
    {
        var services = new ServiceCollection();
        var customProvider = new CustomTimeProvider();
        services.AddSingleton<TimeProvider>(customProvider);

        services.AddStorageQueueTrigger<FakeWorker>(_ => Substitute.For<QueueServiceClient>(), QueueName);

        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<TimeProvider>().ShouldBeSameAs(customProvider);
    }

    [Fact]
    public void AddStorageQueueTrigger_WithBuilder_WhenFactoryIsNull_Throws()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() =>
            services.AddStorageQueueTrigger(
                null!,
                QueueName,
                builder => builder.AddWorker<FakeWorker>()));
    }

    [Fact]
    public void AddStorageQueueTrigger_WithBuilder_WhenQueueNameIsNull_Throws()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() =>
            services.AddStorageQueueTrigger(
                _ => Substitute.For<QueueServiceClient>(),
                null!,
                builder => builder.AddWorker<FakeWorker>()));
    }

    [Fact]
    public void AddStorageQueueTrigger_Generic_WhenFactoryIsNull_Throws()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() =>
            services.AddStorageQueueTrigger<FakeWorker>(null!, QueueName));
    }

    [Fact]
    public void AddStorageQueueTrigger_Typed_WithBuilder_WhenFactoryIsNull_Throws()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() =>
            services.AddStorageQueueTrigger<string>(
                null!,
                QueueName,
                builder => builder.AddWorker<FakeTypedWorker>()));
    }

    [Fact]
    public void AddStorageQueueTrigger_TypedGeneric_WhenFactoryIsNull_Throws()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() =>
            services.AddStorageQueueTrigger<string, FakeTypedWorker>(null!, QueueName));
    }

    private sealed class FakeWorker : IWorker<StorageQueueTriggerContext>
    {
        public Task Execute(StorageQueueTriggerContext context, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeTypedWorker : IWorker<StorageQueueTriggerContext<string>>
    {
        public Task Execute(StorageQueueTriggerContext<string> context, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class CustomTimeProvider : TimeProvider;
}
