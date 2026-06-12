using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using WorkR.Triggers.RunOnce;

namespace WorkR.Tests.Triggers.RunOnce
{
    [Trait("Category", "L0")]
    public class RunOnceTriggerTests
    {
        [Fact]
        public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new RunOnceTrigger(null!, NullLogger<RunOnceTrigger>.Instance));
        }

        [Fact]
        public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new RunOnceTrigger(TimeProvider.System, null!));
        }

        [Fact]
        public async Task ExecuteAsync_CallsWorkerPipelineExactlyOnce()
        {
            var callCount = 0;
            var trigger = Create();

            await trigger.ExecuteAsync((ctx, ct) =>
            {
                callCount++;
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);

            callCount.ShouldBe(1);
        }

        [Fact]
        public async Task ExecuteAsync_PassesCurrentTimestampToWorkerPipeline()
        {
            var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var timeProvider = new FakeTimeProvider(startTime);
            DateTimeOffset? capturedOccurredAt = null;
            var trigger = new RunOnceTrigger(timeProvider, new FakeLogger<RunOnceTrigger>());

            await trigger.ExecuteAsync((ctx, ct) =>
            {
                capturedOccurredAt = ctx.OccurredAt;
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);

            capturedOccurredAt.ShouldBe(startTime);
        }

        [Fact]
        public async Task ExecuteAsync_LogsBeforeCallingWorkerPipeline()
        {
            var logger = new FakeLogger<RunOnceTrigger>();
            var trigger = new RunOnceTrigger(TimeProvider.System, logger);

            await trigger.ExecuteAsync((_, _) => Task.CompletedTask, TestContext.Current.CancellationToken);

            var snapshot = logger.Collector.GetSnapshot();
            snapshot[0].Level.ShouldBe(LogLevel.Information);
            snapshot[0].Message.ShouldContain("initialised");
        }

        [Fact]
        public async Task ExecuteAsync_LogsStoppedAfterWorkerPipelineCompletes()
        {
            var logger = new FakeLogger<RunOnceTrigger>();
            var trigger = new RunOnceTrigger(TimeProvider.System, logger);

            await trigger.ExecuteAsync((_, _) => Task.CompletedTask, TestContext.Current.CancellationToken);

            var snapshot = logger.Collector.GetSnapshot();
            snapshot.Last().Message.ShouldContain("stopped");
        }

        [Fact]
        public async Task ExecuteAsync_WhenWorkerPipelineThrows_DoesNotRethrow()
        {
            var trigger = Create();

            await Should.NotThrowAsync(() =>
                trigger.ExecuteAsync((_, _) => throw new InvalidOperationException(), TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task ExecuteAsync_WhenWorkerPipelineThrows_DoesNotLogError()
        {
            // Worker failures are logged by WorkerService, not the trigger
            var logger = new FakeLogger<RunOnceTrigger>();
            var trigger = new RunOnceTrigger(TimeProvider.System, logger);

            await trigger.ExecuteAsync((_, _) => throw new InvalidOperationException("boom"), TestContext.Current.CancellationToken);

            logger.Collector.GetSnapshot().ShouldNotContain(log => log.Level == LogLevel.Error);
        }

        [Fact]
        public async Task ExecuteAsync_WhenWorkerPipelineThrowsOperationCancelledAndTokenCancelled_Swallows()
        {
            using var cts = new CancellationTokenSource();
            var trigger = Create();

            await Should.NotThrowAsync(() =>
                trigger.ExecuteAsync((_, ct) =>
                {
                    cts.Cancel();
                    ct.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                }, cts.Token));
        }

        private static RunOnceTrigger Create() =>
            new(TimeProvider.System, NullLogger<RunOnceTrigger>.Instance);
    }
}
