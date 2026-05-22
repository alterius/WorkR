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
    public void AddStorageQueueWorker_WithBuilder_RegistersHostedService()
    {
        var services = new ServiceCollection();

        services.AddStorageQueueWorker(
            _ => Substitute.For<QueueServiceClient>(),
            QueueName,
            builder => builder.AddWorker<FakeWorker>());

        services.ShouldContain(d => d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddStorageQueueWorker_Generic_RegistersHostedService()
    {
        var services = new ServiceCollection();

        services.AddStorageQueueWorker<FakeWorker>(_ => Substitute.For<QueueServiceClient>(), QueueName);

        services.ShouldContain(d => d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddStorageQueueWorker_Typed_WithBuilder_RegistersHostedService()
    {
        var services = new ServiceCollection();

        services.AddStorageQueueWorker<string>(
            _ => Substitute.For<QueueServiceClient>(),
            QueueName,
            builder => builder.AddWorker<FakeTypedWorker>());

        services.ShouldContain(d => d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddStorageQueueWorker_TypedGeneric_RegistersHostedService()
    {
        var services = new ServiceCollection();

        services.AddStorageQueueWorker<string, FakeTypedWorker>(_ => Substitute.For<QueueServiceClient>(), QueueName);

        services.ShouldContain(d => d.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void AddStorageQueueWorker_RegistersSystemTimeProviderWhenNoneRegistered()
    {
        var services = new ServiceCollection();

        services.AddStorageQueueWorker<FakeWorker>(_ => Substitute.For<QueueServiceClient>(), QueueName);

        services.ShouldContain(d => d.ServiceType == typeof(TimeProvider));
    }

    [Fact]
    public void AddStorageQueueWorker_DoesNotReplaceAlreadyRegisteredTimeProvider()
    {
        var services = new ServiceCollection();
        var customProvider = new CustomTimeProvider();
        services.AddSingleton<TimeProvider>(customProvider);

        services.AddStorageQueueWorker<FakeWorker>(_ => Substitute.For<QueueServiceClient>(), QueueName);

        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<TimeProvider>().ShouldBeSameAs(customProvider);
    }

    [Fact]
    public void AddStorageQueueWorker_WithBuilder_WhenFactoryIsNull_Throws()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() =>
            services.AddStorageQueueWorker(
                null!,
                QueueName,
                builder => builder.AddWorker<FakeWorker>()));
    }

    [Fact]
    public void AddStorageQueueWorker_WithBuilder_WhenQueueNameIsNull_Throws()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() =>
            services.AddStorageQueueWorker(
                _ => Substitute.For<QueueServiceClient>(),
                null!,
                builder => builder.AddWorker<FakeWorker>()));
    }

    [Fact]
    public void AddStorageQueueWorker_Generic_WhenFactoryIsNull_Throws()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() =>
            services.AddStorageQueueWorker<FakeWorker>(null!, QueueName));
    }

    [Fact]
    public void AddStorageQueueWorker_Typed_WithBuilder_WhenFactoryIsNull_Throws()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() =>
            services.AddStorageQueueWorker<string>(
                null!,
                QueueName,
                builder => builder.AddWorker<FakeTypedWorker>()));
    }

    [Fact]
    public void AddStorageQueueWorker_TypedGeneric_WhenFactoryIsNull_Throws()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() =>
            services.AddStorageQueueWorker<string, FakeTypedWorker>(null!, QueueName));
    }

    private sealed class FakeWorker : IWorker<StorageQueueTriggerContext>
    {
        public Task ExecuteAsync(StorageQueueTriggerContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeTypedWorker : IWorker<StorageQueueTriggerContext<string>>
    {
        public Task ExecuteAsync(StorageQueueTriggerContext<string> context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CustomTimeProvider : TimeProvider;
}
