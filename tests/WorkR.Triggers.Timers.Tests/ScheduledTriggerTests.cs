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
        public async Task ExecuteAsync_WhenWorkerPipelineThrows_LogsError()
        {
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

            logger.Collector.GetSnapshot().ShouldContain(log => log.Level == LogLevel.Error);
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
