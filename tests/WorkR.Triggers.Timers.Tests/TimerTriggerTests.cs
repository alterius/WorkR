using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using NCrontab;
using Shouldly;
using WorkR.Triggers.Timers;

namespace WorkR.Triggers.Timers.Tests
{
    [Trait("Category", "L0")]
    public class TimerTriggerTests
    {
        private static readonly DateTimeOffset StartTime = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private const string EveryMinuteSchedule = "* * * * *";

        [Fact]
        public void Constructor_WhenScheduleIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new TimerTrigger(null!, TimeProvider.System, new FakeLogger<TimerTrigger>()));
        }

        [Fact]
        public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
        {
            var schedule = CrontabSchedule.Parse(EveryMinuteSchedule);
            Should.Throw<ArgumentNullException>(() =>
                new TimerTrigger(schedule, null!, new FakeLogger<TimerTrigger>()));
        }

        [Fact]
        public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
        {
            var schedule = CrontabSchedule.Parse(EveryMinuteSchedule);
            Should.Throw<ArgumentNullException>(() =>
                new TimerTrigger(schedule, TimeProvider.System, null!));
        }

        [Fact]
        public async Task Execute_DoesNotCallNextBeforeScheduledTime()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            var called = false;
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, EveryMinuteSchedule);

            var executeTask = trigger.Execute((ctx, ct) =>
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
        public async Task Execute_WhenRunOnStartupIsTrue_CallsNextImmediately()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            var called = false;
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, EveryMinuteSchedule, runOnStartup: true);

            var executeTask = trigger.Execute((ctx, ct) =>
            {
                called = true;
                return Task.CompletedTask;
            }, cts.Token);

            // Startup call happens synchronously before the while loop's first Task.Delay
            called.ShouldBeTrue();

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task Execute_WhenRunOnStartupIsTrue_PassesCurrentTimestampToNext()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            DateTimeOffset? capturedOccurredAt = null;
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, EveryMinuteSchedule, runOnStartup: true);

            var executeTask = trigger.Execute((ctx, ct) =>
            {
                capturedOccurredAt ??= ctx.OccurredAt;
                return Task.CompletedTask;
            }, cts.Token);

            capturedOccurredAt.ShouldBe(StartTime);

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task Execute_CallsNextAtScheduledTime()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            var called = false;
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, EveryMinuteSchedule);

            var executeTask = trigger.Execute((ctx, ct) =>
            {
                called = true;
                cts.Cancel();
                return Task.CompletedTask;
            }, cts.Token);

            called.ShouldBeFalse();

            timeProvider.Advance(TimeSpan.FromMinutes(1));
            await executeTask;

            called.ShouldBeTrue();
        }

        [Fact]
        public async Task Execute_PassesScheduledTimestampToNext()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            DateTimeOffset? capturedOccurredAt = null;
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, EveryMinuteSchedule);

            var executeTask = trigger.Execute((ctx, ct) =>
            {
                capturedOccurredAt = ctx.OccurredAt;
                cts.Cancel();
                return Task.CompletedTask;
            }, cts.Token);

            timeProvider.Advance(TimeSpan.FromMinutes(1));
            await executeTask;

            // Compute expected the same way the trigger does: GetNextOccurrence returns a DateTime
            // which is then implicitly converted to DateTimeOffset
            var expectedNextOccurrence = CrontabSchedule.Parse(EveryMinuteSchedule)
                .GetNextOccurrence(StartTime.UtcDateTime);
            capturedOccurredAt.ShouldBe(new DateTimeOffset(expectedNextOccurrence));
        }

        [Fact]
        public async Task Execute_StopsWhenCancelled()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, EveryMinuteSchedule);

            var executeTask = trigger.Execute((_, _) => Task.CompletedTask, cts.Token);

            await cts.CancelAsync();

            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task Execute_LogsInitialisationMessage()
        {
            var timeProvider = new FakeTimeProvider(StartTime);
            var logger = new FakeLogger<TimerTrigger>();
            using var cts = new CancellationTokenSource();
            var schedule = CrontabSchedule.Parse(EveryMinuteSchedule);
            var trigger = new TimerTrigger(schedule, timeProvider, logger);

            var executeTask = trigger.Execute((_, _) => Task.CompletedTask, cts.Token);

            // Init log is written synchronously before the first Task.Delay suspension
            logger.Collector.GetSnapshot().ShouldContain(log => log.Level == LogLevel.Information);

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        private static TimerTrigger Create(FakeTimeProvider timeProvider, string schedule, bool runOnStartup = false) =>
            new(CrontabSchedule.Parse(schedule), timeProvider, new FakeLogger<TimerTrigger>(), runOnStartup);
    }
}
