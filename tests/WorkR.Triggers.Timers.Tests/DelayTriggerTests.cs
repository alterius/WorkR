using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Shouldly;

namespace WorkR.Triggers.Timers.Tests
{
    [Trait("Category", "L0")]
    public class DelayTriggerTests
    {
        [Fact]
        public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new DelayTrigger(TimeSpan.FromSeconds(1), null!, new FakeLogger<DelayTrigger>()));
        }

        [Fact]
        public void Constructor_WhenDelayIsZero_ThrowsArgumentOutOfRangeException()
        {
            Should.Throw<ArgumentOutOfRangeException>(() =>
                new DelayTrigger(TimeSpan.Zero, TimeProvider.System, new FakeLogger<DelayTrigger>()));
        }

        [Fact]
        public void Constructor_WhenDelayIsNegative_ThrowsArgumentOutOfRangeException()
        {
            Should.Throw<ArgumentOutOfRangeException>(() =>
                new DelayTrigger(TimeSpan.FromSeconds(-1), TimeProvider.System, new FakeLogger<DelayTrigger>()));
        }

        [Fact]
        public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new DelayTrigger(TimeSpan.FromSeconds(1), TimeProvider.System, null!));
        }

        [Fact]
        public async Task ExecuteAsync_WhenRunOnStartupIsTrue_CallsWorkerPipelineBeforeFirstDelay()
        {
            var timeProvider = new FakeTimeProvider();
            var called = false;
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, TimeSpan.FromSeconds(60), runOnStartup: true);

            var executeTask = trigger.ExecuteAsync((ctx, ct) =>
            {
                called = true;
                return Task.CompletedTask;
            }, cts.Token);

            // workerPipeline is called synchronously during ExecuteAsync() before the first Task.Delay suspension
            called.ShouldBeTrue();

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task ExecuteAsync_WhenRunOnStartupIsFalse_DoesNotCallWorkerPipelineBeforeFirstDelay()
        {
            var timeProvider = new FakeTimeProvider();
            var called = false;
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, TimeSpan.FromSeconds(60), runOnStartup: false);

            var executeTask = trigger.ExecuteAsync((ctx, ct) =>
            {
                called = true;
                return Task.CompletedTask;
            }, cts.Token);

            // Trigger is suspended in Task.Delay, waiting for the first delay to elapse
            called.ShouldBeFalse();

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task ExecuteAsync_WhenRunOnStartupIsFalse_CallsWorkerPipelineAfterFirstDelay()
        {
            var timeProvider = new FakeTimeProvider();
            var called = false;
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, TimeSpan.FromSeconds(60), runOnStartup: false);

            var executeTask = trigger.ExecuteAsync((ctx, ct) =>
            {
                called = true;
                cts.Cancel();
                return Task.CompletedTask;
            }, cts.Token);

            called.ShouldBeFalse();

            timeProvider.Advance(TimeSpan.FromSeconds(60));
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

            called.ShouldBeTrue();
        }

        [Fact]
        public async Task ExecuteAsync_PassesCurrentTimestampToWorkerPipeline()
        {
            var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var timeProvider = new FakeTimeProvider(startTime);
            DateTimeOffset? capturedOccurredAt = null;
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, TimeSpan.FromSeconds(60), runOnStartup: true);

            var executeTask = trigger.ExecuteAsync((ctx, ct) =>
            {
                capturedOccurredAt = ctx.OccurredAt;
                return Task.CompletedTask;
            }, cts.Token);

            capturedOccurredAt.ShouldBe(startTime);

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task ExecuteAsync_WaitsDelayBetweenInvocations()
        {
            var timeProvider = new FakeTimeProvider();
            var callCount = 0;
            var secondCallStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, TimeSpan.FromSeconds(60), runOnStartup: true);

            var executeTask = trigger.ExecuteAsync((ctx, ct) =>
            {
                callCount++;
                if (callCount == 2)
                    secondCallStarted.TrySetResult();
                return Task.CompletedTask;
            }, cts.Token);

            // First call happened synchronously; the delay timer is now pending
            callCount.ShouldBe(1);

            timeProvider.Advance(TimeSpan.FromSeconds(60));
            await secondCallStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task ExecuteAsync_StopsWhenCancelled()
        {
            var timeProvider = new FakeTimeProvider();
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, TimeSpan.FromSeconds(60), runOnStartup: true);

            var executeTask = trigger.ExecuteAsync((_, _) => Task.CompletedTask, cts.Token);

            await cts.CancelAsync();

            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task ExecuteAsync_LogsInitialisationMessage()
        {
            var timeProvider = new FakeTimeProvider();
            var logger = new FakeLogger<DelayTrigger>();
            using var cts = new CancellationTokenSource();
            var trigger = new DelayTrigger(TimeSpan.FromSeconds(60), timeProvider, logger);

            var executeTask = trigger.ExecuteAsync((_, _) => Task.CompletedTask, cts.Token);

            // Init log is written synchronously before the first Task.Delay suspension
            logger.Collector.GetSnapshot().ShouldContain(log => log.Level == LogLevel.Information);

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task ExecuteAsync_WhenTokenAlreadyCancelled_DoesNotCallWorkerPipeline()
        {
            var timeProvider = new FakeTimeProvider();
            var called = false;
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            var trigger = Create(timeProvider, TimeSpan.FromSeconds(60), runOnStartup: true);

            await trigger.ExecuteAsync((_, _) =>
            {
                called = true;
                return Task.CompletedTask;
            }, cts.Token);

            called.ShouldBeFalse();
        }

        [Fact]
        public async Task ExecuteAsync_LogsStoppedOnCancellation()
        {
            var timeProvider = new FakeTimeProvider();
            var logger = new FakeLogger<DelayTrigger>();
            using var cts = new CancellationTokenSource();
            var trigger = new DelayTrigger(TimeSpan.FromSeconds(60), timeProvider, logger);

            var executeTask = trigger.ExecuteAsync((_, _) => Task.CompletedTask, cts.Token);

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

            logger.Collector.GetSnapshot().ShouldContain(log => log.Message.Contains("stopped"));
        }

        [Fact]
        public async Task ExecuteAsync_WhenWorkerPipelineThrows_DoesNotStopLoop()
        {
            var timeProvider = new FakeTimeProvider();
            var callCount = 0;
            var secondCallDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource();

            var trigger = Create(timeProvider, TimeSpan.FromSeconds(60), runOnStartup: true);

            var executeTask = trigger.ExecuteAsync((_, _) =>
            {
                callCount++;
                if (callCount >= 2)
                    secondCallDone.TrySetResult();
                throw new InvalidOperationException();
            }, cts.Token);

            // First call threw; trigger is now suspended in Task.Delay — advance to fire second call
            timeProvider.Advance(TimeSpan.FromSeconds(60));
            await secondCallDone.Task.WaitAsync(TestContext.Current.CancellationToken);

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

            callCount.ShouldBe(2);
        }

        [Fact]
        public async Task ExecuteAsync_WhenWorkerPipelineThrows_DoesNotLogError()
        {
            // Worker failures are logged by WorkerService, not the trigger
            var timeProvider = new FakeTimeProvider();
            var logger = new FakeLogger<DelayTrigger>();
            var workerThrew = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource();
            var trigger = new DelayTrigger(TimeSpan.FromSeconds(60), timeProvider, logger, runOnStartup: true);

            var executeTask = trigger.ExecuteAsync((_, _) =>
            {
                workerThrew.TrySetResult();
                throw new InvalidOperationException("boom");
            }, cts.Token);

            await workerThrew.Task.WaitAsync(TestContext.Current.CancellationToken);
            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

            logger.Collector.GetSnapshot().ShouldNotContain(log => log.Level == LogLevel.Error);
        }

        [Fact]
        public async Task ExecuteAsync_WhenWorkerPipelineThrowsOperationCancelledAndTokenCancelled_Propagates()
        {
            var timeProvider = new FakeTimeProvider();
            using var cts = new CancellationTokenSource();

            var trigger = Create(timeProvider, TimeSpan.FromSeconds(60), runOnStartup: true);

            var executeTask = trigger.ExecuteAsync((_, ct) =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }, cts.Token);

            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        private static DelayTrigger Create(
            FakeTimeProvider timeProvider,
            TimeSpan delay,
            bool runOnStartup = false) =>
            new(delay, timeProvider, new FakeLogger<DelayTrigger>(), runOnStartup);
    }
}
