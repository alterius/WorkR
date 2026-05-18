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
        public async Task Execute_CallsNextExactlyOnce()
        {
            var callCount = 0;
            var trigger = Create();

            await trigger.Execute((ctx, ct) =>
            {
                callCount++;
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);

            callCount.ShouldBe(1);
        }

        [Fact]
        public async Task Execute_PassesCurrentTimestampToNext()
        {
            var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var timeProvider = new FakeTimeProvider(startTime);
            DateTimeOffset? capturedOccurredAt = null;
            var trigger = new RunOnceTrigger(timeProvider, new FakeLogger<RunOnceTrigger>());

            await trigger.Execute((ctx, ct) =>
            {
                capturedOccurredAt = ctx.OccurredAt;
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);

            capturedOccurredAt.ShouldBe(startTime);
        }

        [Fact]
        public async Task Execute_LogsBeforeCallingNext()
        {
            var logger = new FakeLogger<RunOnceTrigger>();
            var trigger = new RunOnceTrigger(TimeProvider.System, logger);

            // When next throws, only the pre-next log is recorded
            await Should.ThrowAsync<InvalidOperationException>(() =>
                trigger.Execute((_, _) => Task.FromException(new InvalidOperationException()), TestContext.Current.CancellationToken));

            logger.Collector.GetSnapshot().ShouldHaveSingleItem()
                .Level.ShouldBe(LogLevel.Information);
        }

        [Fact]
        public async Task Execute_LogsAfterNextCompletes()
        {
            var logger = new FakeLogger<RunOnceTrigger>();
            var trigger = new RunOnceTrigger(TimeProvider.System, logger);

            await trigger.Execute((_, _) => Task.CompletedTask, TestContext.Current.CancellationToken);

            logger.Collector.GetSnapshot().Count.ShouldBe(2);
        }

        private static RunOnceTrigger Create() =>
            new(TimeProvider.System, NullLogger<RunOnceTrigger>.Instance);
    }
}
