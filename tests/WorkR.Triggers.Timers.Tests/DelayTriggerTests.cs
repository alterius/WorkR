using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using WorkR.Triggers.Timers;

namespace WorkR.Triggers.Timers.Tests
{
    [Trait("Category", "L0")]
    public class DelayTriggerTests
    {
        [Fact]
        public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new DelayTrigger(null!, TimeSpan.FromSeconds(1), new FakeLogger<DelayTrigger>()));
        }

        [Fact]
        public void Constructor_WhenDelayIsZero_ThrowsArgumentOutOfRangeException()
        {
            Should.Throw<ArgumentOutOfRangeException>(() =>
                new DelayTrigger(TimeProvider.System, TimeSpan.Zero, new FakeLogger<DelayTrigger>()));
        }

        [Fact]
        public void Constructor_WhenDelayIsNegative_ThrowsArgumentOutOfRangeException()
        {
            Should.Throw<ArgumentOutOfRangeException>(() =>
                new DelayTrigger(TimeProvider.System, TimeSpan.FromSeconds(-1), new FakeLogger<DelayTrigger>()));
        }

        [Fact]
        public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new DelayTrigger(TimeProvider.System, TimeSpan.FromSeconds(1), null!));
        }

        [Fact]
        public async Task Execute_CallsNextBeforeFirstDelay()
        {
            var timeProvider = new FakeTimeProvider();
            var called = false;
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, TimeSpan.FromSeconds(60));

            var executeTask = trigger.Execute((ctx, ct) =>
            {
                called = true;
                return Task.CompletedTask;
            }, cts.Token);

            // next is called synchronously during Execute() before the first Task.Delay suspension
            called.ShouldBeTrue();

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task Execute_PassesCurrentTimestampToNext()
        {
            var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var timeProvider = new FakeTimeProvider(startTime);
            DateTimeOffset? capturedOccurredAt = null;
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, TimeSpan.FromSeconds(60));

            var executeTask = trigger.Execute((ctx, ct) =>
            {
                capturedOccurredAt = ctx.OccurredAt;
                return Task.CompletedTask;
            }, cts.Token);

            capturedOccurredAt.ShouldBe(startTime);

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task Execute_WaitsDelayBetweenInvocations()
        {
            var timeProvider = new FakeTimeProvider();
            var callCount = 0;
            var secondCallStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, TimeSpan.FromSeconds(60));

            var executeTask = trigger.Execute((ctx, ct) =>
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
        public async Task Execute_StopsWhenCancelled()
        {
            var timeProvider = new FakeTimeProvider();
            using var cts = new CancellationTokenSource();
            var trigger = Create(timeProvider, TimeSpan.FromSeconds(60));

            var executeTask = trigger.Execute((_, _) => Task.CompletedTask, cts.Token);

            await cts.CancelAsync();

            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        [Fact]
        public async Task Execute_LogsInitialisationMessage()
        {
            var timeProvider = new FakeTimeProvider();
            var logger = new FakeLogger<DelayTrigger>();
            using var cts = new CancellationTokenSource();
            var trigger = new DelayTrigger(timeProvider, TimeSpan.FromSeconds(60), logger);

            var executeTask = trigger.Execute((_, _) => Task.CompletedTask, cts.Token);

            // Init log is written synchronously before the first Task.Delay suspension
            logger.Collector.GetSnapshot().ShouldContain(log => log.Level == LogLevel.Information);

            await cts.CancelAsync();
            await Should.ThrowAsync<OperationCanceledException>(() => executeTask);
        }

        private static DelayTrigger Create(FakeTimeProvider timeProvider, TimeSpan delay) =>
            new(timeProvider, delay, new FakeLogger<DelayTrigger>());
    }
}
