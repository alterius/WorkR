using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using NCrontab;
using Shouldly;

namespace WorkR.Triggers.Timers.Tests
{
    [Trait("Category", "L0")]
    public class ScheduledTriggerTests
    {
        private static readonly DateTimeOffset StartTime = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private const string EveryMinuteSchedule = "* * * * *";

        [Fact]
        public void Constructor_WhenScheduleIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new ScheduledTrigger(null!, TimeProvider.System, new FakeLogger<ScheduledTrigger>()));
        }

        [Fact]
        public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
        {
            var schedule = CrontabSchedule.Parse(EveryMinuteSchedule);
            Should.Throw<ArgumentNullException>(() =>
                new ScheduledTrigger(schedule, null!, new FakeLogger<ScheduledTrigger>()));
        }

        [Fact]
        public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
        {
            var schedule = CrontabSchedule.Parse(EveryMinuteSchedule);
            Should.Throw<ArgumentNullException>(() =>
                new ScheduledTrigger(schedule, TimeProvider.System, null!));
        }

        [Fact]
        public async Task ExecuteAsync_DoesNotCallWorkerPipelineBeforeScheduledTime()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            var called = false;
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, EveryMinuteSchedule);

            var executeTask = trigger.ExecuteAsync((ctx, ct) =>
            {
                called = true;
                return Task.CompletedTask;
            }, cts.Token);

            // Trigger is suspended in Task.Delay, waiting for the next cron occurrence
            called.ShouldBeFalse();

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task ExecuteAsync_WhenRunOnStartupIsTrue_CallsWorkerPipelineImmediately()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            var workerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, EveryMinuteSchedule, runOnStartup: true);

            var executeTask = trigger.ExecuteAsync((ctx, ct) =>
            {
                workerInvoked.TrySetResult();
                return Task.CompletedTask;
            }, cts.Token);

            await workerInvoked.Task.WaitAsync(TestContext.Current.CancellationToken);

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task ExecuteAsync_WhenRunOnStartupIsTrue_PassesCurrentTimestampToWorkerPipeline()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            DateTimeOffset? capturedOccurredAt = null;
            var workerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, EveryMinuteSchedule, runOnStartup: true);

            var executeTask = trigger.ExecuteAsync((ctx, ct) =>
            {
                capturedOccurredAt ??= ctx.OccurredAt;
                workerInvoked.TrySetResult();
                return Task.CompletedTask;
            }, cts.Token);

            await workerInvoked.Task.WaitAsync(TestContext.Current.CancellationToken);

            capturedOccurredAt.ShouldBe(StartTime);

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task ExecuteAsync_CallsWorkerPipelineAtScheduledTime()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            var workerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, EveryMinuteSchedule);

            var executeTask = trigger.ExecuteAsync((ctx, ct) =>
            {
                workerInvoked.TrySetResult();
                return Task.CompletedTask;
            }, cts.Token);

            workerInvoked.Task.IsCompleted.ShouldBeFalse();

            timeProvider.Advance(TimeSpan.FromMinutes(1));
            await workerInvoked.Task.WaitAsync(TestContext.Current.CancellationToken);

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task ExecuteAsync_PassesScheduledTimestampToWorkerPipeline()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            DateTimeOffset? capturedOccurredAt = null;
            var workerInvoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, EveryMinuteSchedule);

            var executeTask = trigger.ExecuteAsync((ctx, ct) =>
            {
                capturedOccurredAt = ctx.OccurredAt;
                workerInvoked.TrySetResult();
                return Task.CompletedTask;
            }, cts.Token);

            timeProvider.Advance(TimeSpan.FromMinutes(1));
            await workerInvoked.Task.WaitAsync(TestContext.Current.CancellationToken);

            // Compute expected the same way the trigger does: GetNextOccurrence returns a DateTime
            // which is then implicitly converted to DateTimeOffset
            var expectedNextOccurrence = CrontabSchedule.Parse(EveryMinuteSchedule)
                .GetNextOccurrence(StartTime.UtcDateTime);
            capturedOccurredAt.ShouldBe(new DateTimeOffset(expectedNextOccurrence));

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task ExecuteAsync_StopsWhenCancelled()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, EveryMinuteSchedule);

            var executeTask = trigger.ExecuteAsync((_, _) => Task.CompletedTask, cts.Token);

            await cts.CancelAsync();

            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task ExecuteAsync_LogsInitialisationMessage()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            var logger = new FakeLogger<ScheduledTrigger>();
            using var cts = new CancellationTokenSource();
            var schedule = CrontabSchedule.Parse(EveryMinuteSchedule);
            var trigger = new ScheduledTrigger(schedule, timeProvider, logger);

            var executeTask = trigger.ExecuteAsync((_, _) => Task.CompletedTask, cts.Token);

            // Init log is written synchronously before the first Task.Delay suspension
            logger.Collector.GetSnapshot().ShouldContain(log => log.Level == LogLevel.Information);

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task ExecuteAsync_WhenTokenAlreadyCancelled_DoesNotCallWorkerPipeline()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            var called = false;
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            var trigger = Create(timeProvider, EveryMinuteSchedule);

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
            var timeProvider = new FakeTimeProvider(StartTime);
            var logger = new FakeLogger<ScheduledTrigger>();
            using var cts = new CancellationTokenSource();
            var trigger = new ScheduledTrigger(CrontabSchedule.Parse(EveryMinuteSchedule), timeProvider, logger);

            var executeTask = trigger.ExecuteAsync((_, _) => Task.CompletedTask, cts.Token);

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

            logger.Collector.GetSnapshot().ShouldContain(log => log.Message.Contains("stopped"));
        }

        [Fact]
        public async Task ExecuteAsync_WhenWorkerPipelineThrows_DoesNotStopLoop()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            var callCount = 0;
            var secondCallDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource();

            var trigger = Create(timeProvider, EveryMinuteSchedule);

            var executeTask = trigger.ExecuteAsync((_, _) =>
            {
                if (Interlocked.Increment(ref callCount) >= 2)
                    secondCallDone.TrySetResult();
                throw new InvalidOperationException();
            }, cts.Token);

            timeProvider.Advance(TimeSpan.FromMinutes(1));
            timeProvider.Advance(TimeSpan.FromMinutes(1));
            await secondCallDone.Task.WaitAsync(TestContext.Current.CancellationToken);

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

            callCount.ShouldBeGreaterThanOrEqualTo(2);
        }

        [Fact]
        public async Task ExecuteAsync_WhenWorkerPipelineThrows_DoesNotLogError()
        {
            // Worker failures are logged by WorkerService, not the trigger
            var timeProvider = new FakeTimeProvider(StartTime);
            var logger = new FakeLogger<ScheduledTrigger>();
            var workerThrew = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource();
            var trigger = new ScheduledTrigger(CrontabSchedule.Parse(EveryMinuteSchedule), timeProvider, logger, runOnStartup: true);

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
            var timeProvider = new FakeTimeProvider(StartTime);
            using var cts = new CancellationTokenSource();

            var trigger = Create(timeProvider, EveryMinuteSchedule, runOnStartup: true);

            var executeTask = trigger.ExecuteAsync((_, ct) =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }, cts.Token);

            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task ExecuteAsync_WhenCancelOnOverlapIsTrue_CancelsRunningExecutionOnNextFiring()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var logger = new FakeLogger<ScheduledTrigger>();
            using var cts = new CancellationTokenSource();
            var trigger = new ScheduledTrigger(CrontabSchedule.Parse(EveryMinuteSchedule), timeProvider,
                logger, cancelOnOverlap: true);

            var executeTask = trigger.ExecuteAsync(async (ctx, ct) =>
            {
                firstStarted.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
            }, cts.Token);

            timeProvider.Advance(TimeSpan.FromMinutes(1));
            await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

            timeProvider.Advance(TimeSpan.FromMinutes(1));

            // Poll until Next() has caught the OperationCanceledException and logged the warning
            var warningLogged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = Task.Run(async () =>
            {
                while (!logger.Collector.GetSnapshot().Any(l => l.Level == LogLevel.Warning))
                    await Task.Delay(10, TestContext.Current.CancellationToken);
                warningLogged.TrySetResult();
            }, TestContext.Current.CancellationToken);
            await warningLogged.Task.WaitAsync(TestContext.Current.CancellationToken);

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

            logger.Collector.GetSnapshot().ShouldContain(log => log.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task ExecuteAsync_WhenCancelOnOverlapIsFalse_DoesNotCancelRunningExecution()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var callCount = 0;
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, EveryMinuteSchedule);

            var executeTask = trigger.ExecuteAsync(async (ctx, ct) =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                {
                    firstStarted.TrySetResult();
                    await firstGate.Task;
                }
                else
                {
                    secondStarted.TrySetResult();
                }
            }, cts.Token);

            timeProvider.Advance(TimeSpan.FromMinutes(1));
            await firstStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

            timeProvider.Advance(TimeSpan.FromMinutes(1));
            await secondStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

            firstGate.TrySetResult();
            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task ExecuteAsync_OnShutdown_AwaitsInFlightExecutionsBeforeStopping()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            var workerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var workerGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var workerCompleted = false;
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, EveryMinuteSchedule);

            var executeTask = trigger.ExecuteAsync(async (ctx, ct) =>
            {
                workerStarted.TrySetResult();
                await workerGate.Task.WaitAsync(TestContext.Current.CancellationToken);
                workerCompleted = true;
            }, cts.Token);

            timeProvider.Advance(TimeSpan.FromMinutes(1));
            await workerStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

            await cts.CancelAsync();
            workerCompleted.ShouldBeFalse();

            workerGate.TrySetResult();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

            workerCompleted.ShouldBeTrue();
        }

        [Fact]
        public async Task ExecuteAsync_WhenCancelOnOverlapIsFalse_ShutdownDoesNotLogWarning()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            var logger = new FakeLogger<ScheduledTrigger>();
            using var cts = new CancellationTokenSource();
            var trigger = new ScheduledTrigger(CrontabSchedule.Parse(EveryMinuteSchedule), timeProvider, logger);

            var executeTask = trigger.ExecuteAsync(async (ctx, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
            }, cts.Token);

            timeProvider.Advance(TimeSpan.FromMinutes(1));
            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);

            logger.Collector.GetSnapshot().ShouldNotContain(log => log.Level == LogLevel.Warning);
        }

        private static ScheduledTrigger Create(
            FakeTimeProvider timeProvider,
            string schedule,
            bool runOnStartup = false) =>
            new(CrontabSchedule.Parse(schedule),
                timeProvider,
                new FakeLogger<ScheduledTrigger>(),
                runOnStartup);
    }
}
