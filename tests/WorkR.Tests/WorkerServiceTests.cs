using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;

namespace WorkR.Tests
{
    [Trait("Category", "L0")]
    public class WorkerServiceTests
    {
        [Fact]
        public void Constructor_WhenTriggerIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new WorkerService<FakeTrigger, EmptyTriggerContext>(
                    null!,
                    MakePipeline(),
                    FakeFactory()));
        }

        [Fact]
        public void Constructor_WhenWorkerPipelineIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new WorkerService<FakeTrigger, EmptyTriggerContext>(
                    new FakeTrigger(),
                    null!,
                    FakeFactory()));
        }

        [Fact]
        public void Constructor_WhenLoggerFactoryIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new WorkerService<FakeTrigger, EmptyTriggerContext>(
                    new FakeTrigger(),
                    MakePipeline(),
                    null!));
        }

        [Fact]
        public async Task ExecuteAsync_CallsTrigger()
        {
            var called = false;
            var service = Create(new FakeTrigger((next, ct) =>
            {
                called = true;
                return Task.CompletedTask;
            }));

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            called.ShouldBeTrue();
        }

        [Fact]
        public async Task ExecuteAsync_LogsStartAndStop()
        {
            var provider = new FakeLoggerProvider();
            var service = Create(loggerProvider: provider);

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            provider.Collector.GetSnapshot()
                .Count(l => l.Level == LogLevel.Information)
                .ShouldBe(3); // starting, started, stopped
        }

        [Fact]
        public async Task ExecuteAsync_WhenCancelledDuringShutdown_DoesNotPropagate()
        {
            var triggerRunning = new SemaphoreSlim(0, 1);
            var service = Create(new FakeTrigger(async (next, ct) =>
            {
                triggerRunning.Release();
                await Task.Delay(Timeout.Infinite, ct);
            }));

            await service.StartAsync(CancellationToken.None);
            await triggerRunning.WaitAsync(TestContext.Current.CancellationToken);
            await service.StopAsync(CancellationToken.None);

            service.ExecuteTask!.IsCompletedSuccessfully.ShouldBeTrue();
        }

        [Fact]
        public async Task ExecuteAsync_WhenCancelledDuringShutdown_LogsShuttingDown()
        {
            var provider = new FakeLoggerProvider();
            var triggerRunning = new SemaphoreSlim(0, 1);
            var service = Create(
                new FakeTrigger(async (next, ct) =>
                {
                    triggerRunning.Release();
                    await Task.Delay(Timeout.Infinite, ct);
                }),
                provider);

            await service.StartAsync(CancellationToken.None);
            await triggerRunning.WaitAsync(TestContext.Current.CancellationToken);
            await service.StopAsync(CancellationToken.None);

            provider.Collector.GetSnapshot()
                .Count(l => l.Level == LogLevel.Information)
                .ShouldBe(4); // starting, started, shutting down, stopped
        }

        [Fact]
        public async Task ExecuteAsync_WhenTriggerThrowsOperationCanceledException_WithoutCancelledToken_FaultsExecuteTask()
        {
            var service = Create(new FakeTrigger((next, ct) =>
                Task.FromException(new OperationCanceledException())));

            // .NET 10 no longer propagates a synchronously-faulted ExecuteAsync through StartAsync
            try
            {
                await service.StartAsync(CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
            }

            await Should.ThrowAsync<OperationCanceledException>(() =>
                service.ExecuteTask!.WaitAsync(TestContext.Current.CancellationToken));
        }

        private static WorkerService<FakeTrigger, EmptyTriggerContext> Create(
            FakeTrigger? trigger = null,
            FakeLoggerProvider? loggerProvider = null) =>
            new(trigger ?? new FakeTrigger(),
                MakePipeline(),
                new LoggerFactory([loggerProvider ?? new FakeLoggerProvider()]));

        private static LoggerFactory FakeFactory() =>
            new([new FakeLoggerProvider()]);

        private static IWorkerPipeline<EmptyTriggerContext> MakePipeline() =>
            new WorkerPipelineBuilder<EmptyTriggerContext>(["FakeWorker"], (_, _, _) => Task.CompletedTask)
                .Build(EmptyServiceProvider.Instance);

        private sealed class EmptyServiceProvider : IServiceProvider
        {
            public static readonly EmptyServiceProvider Instance = new();

            public object? GetService(Type serviceType) => null;
        }

        private sealed class FakeTrigger : ITrigger<EmptyTriggerContext>
        {
            private readonly Func<IWorkerPipeline<EmptyTriggerContext>, CancellationToken, Task> _execute;

            public FakeTrigger(
                Func<IWorkerPipeline<EmptyTriggerContext>, CancellationToken, Task>? execute = null)
            {
                _execute = execute ?? ((_, _) => Task.CompletedTask);
            }

            public Task ExecuteAsync(IWorkerPipeline<EmptyTriggerContext> workerPipeline, CancellationToken stoppingToken) =>
                _execute(workerPipeline, stoppingToken);
        }

        private sealed class FakeWorker : IWorker<EmptyTriggerContext>
        {
            public Task ExecuteAsync(EmptyTriggerContext source, CancellationToken cancellationToken) =>
                Task.CompletedTask;
        }
    }
}
