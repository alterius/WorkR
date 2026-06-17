using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;

namespace WorkR.Tests.Hosting
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
                    TestPipeline.Named(),
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
                    TestPipeline.Named(),
                    null!));
        }

        [Fact]
        public async Task ExecuteAsync_CallsTrigger()
        {
            var called = false;
            var service = Create(new FakeTrigger((_, _) =>
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
            var service = Create(new FakeTrigger(async (_, ct) =>
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
                new FakeTrigger(async (_, ct) =>
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
            var service = Create(new FakeTrigger((_, _) =>
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

        [Fact]
        public async Task ExecuteAsync_BeginsServiceScopeWithVersionTriggerAndPipeline()
        {
            var provider = new FakeLoggerProvider();
            var service = Create(loggerProvider: provider);

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            var starting = provider.Collector.GetSnapshot()
                .Where(log => log.Message == "Worker service starting...")
                .ShouldHaveSingleItem();

            var scope = starting.Scopes
                .OfType<IEnumerable<KeyValuePair<string, object?>>>()
                .SelectMany(s => s)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            scope["WorkRVersion"].ShouldBe(typeof(WorkerService<,>).Assembly.GetName().Version!.ToString());
            scope["WorkerServiceId"].ShouldBeOfType<Guid>();
            scope["Trigger"].ShouldBe(nameof(FakeTrigger));
            scope["TriggerVersion"].ShouldBe(typeof(FakeTrigger).Assembly.GetName().Version!.ToString());
            scope["WorkerPipeline"].ShouldBe("FakeWorker");
        }

        private static WorkerService<FakeTrigger, EmptyTriggerContext> Create(
            FakeTrigger? trigger = null,
            FakeLoggerProvider? loggerProvider = null) =>
            new(trigger ?? new FakeTrigger(),
                TestPipeline.Named(),
                new LoggerFactory([loggerProvider ?? new FakeLoggerProvider()]));

        private static LoggerFactory FakeFactory() =>
            new([new FakeLoggerProvider()]);
    }
}
