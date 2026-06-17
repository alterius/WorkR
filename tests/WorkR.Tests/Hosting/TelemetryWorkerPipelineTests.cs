using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;

namespace WorkR.Tests.Hosting
{
    [Trait("Category", "L0")]
    public class TelemetryWorkerPipelineTests
    {
        private static readonly string WorkRVersion = typeof(WorkerService<,>).Assembly.GetName().Version!.ToString();

        [Fact]
        public async Task ExecuteAsync_WhenListenerSubscribed_CreatesActivityPerExecution()
        {
            var trigger = NewMarker();
            var activities = new List<Activity>();
            using var listener = Listen(activities);

            var first = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            var second = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            var serviceId = Guid.NewGuid();
            var pipeline = Create(
                TestPipeline.Named("FakeWorker"),
                trigger: trigger,
                serviceId: serviceId,
                workerVersion: WorkRVersion,
                triggerVersion: "1.2.3.4");

            await pipeline.ExecuteAsync(first, TestContext.Current.CancellationToken);
            await pipeline.ExecuteAsync(second, TestContext.Current.CancellationToken);

            var mine = Mine(activities, trigger);
            mine.Count.ShouldBe(2);
            mine.ShouldAllBe(a => a.OperationName == "EXECUTE FakeWorker");
            mine[0].GetTagItem("workr.execution.id").ShouldBe(first.ExecutionId);
            mine[1].GetTagItem("workr.execution.id").ShouldBe(second.ExecutionId);
            mine.ShouldAllBe(a => Equals(a.GetTagItem("workr.service.id"), serviceId));
            mine.ShouldAllBe(a => Equals(a.GetTagItem("workr.trigger.version"), "1.2.3.4"));
            mine.ShouldAllBe(a => Equals(a.GetTagItem("workr.version"), WorkRVersion));
            mine.ShouldAllBe(a => Equals(a.GetTagItem("workr.pipeline"), "FakeWorker"));
            mine.ShouldAllBe(a => a.Source.Version == WorkRVersion);
        }

        [Fact]
        public async Task ExecuteAsync_ActivityNameIsExecuteFollowedByPipeline()
        {
            var trigger = NewMarker();
            var activities = new List<Activity>();
            using var listener = Listen(activities);

            var pipeline = Create(TestPipeline.Named(["FakeWorker", "OtherFakeWorker"]), trigger: trigger);

            await pipeline.ExecuteAsync(new EmptyTriggerContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

            var activity = Mine(activities, trigger).ShouldHaveSingleItem();
            activity.OperationName.ShouldBe("EXECUTE FakeWorker -> OtherFakeWorker");
            activity.GetTagItem("workr.pipeline").ShouldBe("FakeWorker -> OtherFakeWorker");
        }

        [Fact]
        public async Task ExecuteAsync_UsesPipelineNameVerbatim()
        {
            var trigger = NewMarker();
            var activities = new List<Activity>();
            using var listener = Listen(activities);

            var pipeline = Create(TestPipeline.Named("GenericFakeWorker<string>"), trigger: trigger);

            await pipeline.ExecuteAsync(new EmptyTriggerContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

            var activity = Mine(activities, trigger).ShouldHaveSingleItem();
            activity.GetTagItem("workr.pipeline").ShouldBe("GenericFakeWorker<string>");
            activity.OperationName.ShouldBe("EXECUTE GenericFakeWorker<string>");
        }

        [Fact]
        public async Task ExecuteAsync_WhenWorkerThrows_MarksActivityAsErrorAndPropagates()
        {
            var trigger = NewMarker();
            var activities = new List<Activity>();
            using var listener = Listen(activities);

            var pipeline = Create(
                TestPipeline.Named("FakeWorker", (_, _) => throw new InvalidOperationException("boom")),
                trigger: trigger);

            var caught = await Should.ThrowAsync<InvalidOperationException>(() =>
                pipeline.ExecuteAsync(new EmptyTriggerContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken));
            caught.Message.ShouldBe("boom");

            var activity = Mine(activities, trigger).ShouldHaveSingleItem();
            activity.Status.ShouldBe(ActivityStatusCode.Error);
            activity.StatusDescription.ShouldBe("boom");
            activity.GetTagItem("error.type").ShouldBe(typeof(InvalidOperationException).FullName);
            activity.Events.ShouldContain(e => e.Name == "exception");
        }

        [Fact]
        public async Task ExecuteAsync_WhenExecutionCancelled_DoesNotMarkActivityAsError()
        {
            var trigger = NewMarker();
            var activities = new List<Activity>();
            using var listener = Listen(activities);

            using var executionCts = new CancellationTokenSource();
            var context = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            var pipeline = Create(
                TestPipeline.Named("FakeWorker", (_, ct) =>
                {
                    executionCts.Cancel();
                    ct.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                }),
                trigger: trigger);

            await Should.ThrowAsync<OperationCanceledException>(() =>
                pipeline.ExecuteAsync(context, executionCts.Token));

            var activity = Mine(activities, trigger).ShouldHaveSingleItem();
            activity.Status.ShouldBe(ActivityStatusCode.Unset);
            activity.GetTagItem("error.type").ShouldBeNull();
            activity.Events.ShouldNotContain(e => e.Name == "exception");
        }

        [Fact]
        public async Task ExecuteAsync_WhenNoListenerSubscribed_DoesNotCreateActivity()
        {
            Activity? observed = null;
            var executed = false;
            var pipeline = Create(TestPipeline.Named("FakeWorker", (_, _) =>
            {
                observed = Activity.Current;
                executed = true;
                return Task.CompletedTask;
            }));

            await pipeline.ExecuteAsync(new EmptyTriggerContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

            executed.ShouldBeTrue();
            observed.ShouldBeNull();
        }

        [Fact]
        public async Task ExecuteAsync_LogsWorkerExecutingAndExecuted_WithExecutionIdScope()
        {
            var logger = new FakeLogger();
            var context = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            var pipeline = Create(TestPipeline.Named(), logger);

            await pipeline.ExecuteAsync(context, TestContext.Current.CancellationToken);

            var snapshot = logger.Collector.GetSnapshot();

            var executing = snapshot.Where(log => log.Message == "Worker pipeline executing...").ShouldHaveSingleItem();
            executing.Level.ShouldBe(LogLevel.Debug);
            executing.Scopes
                .OfType<IEnumerable<KeyValuePair<string, object?>>>()
                .SelectMany(scope => scope)
                .ShouldContain(new KeyValuePair<string, object?>("ExecutionId", context.ExecutionId));

            var executed = snapshot.Where(log => log.Message == "Worker pipeline executed").ShouldHaveSingleItem();
            executed.Level.ShouldBe(LogLevel.Debug);
        }

        [Fact]
        public async Task ExecuteAsync_WhenWorkerThrows_LogsError()
        {
            var logger = new FakeLogger();
            var pipeline = Create(
                TestPipeline.Named("FakeWorker", (_, _) => throw new InvalidOperationException("boom")),
                logger);

            await Should.ThrowAsync<InvalidOperationException>(() =>
                pipeline.ExecuteAsync(new EmptyTriggerContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken));

            var error = logger.Collector.GetSnapshot().Where(log => log.Level == LogLevel.Error).ShouldHaveSingleItem();
            error.Message.ShouldBe("Worker pipeline execution failed");
            error.Exception.ShouldBeOfType<InvalidOperationException>();
        }

        [Fact]
        public async Task ExecuteAsync_WhenExecutionCancelled_DoesNotLogError()
        {
            var logger = new FakeLogger();
            using var executionCts = new CancellationTokenSource();
            var pipeline = Create(
                TestPipeline.Named("FakeWorker", (_, ct) =>
                {
                    executionCts.Cancel();
                    ct.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                }),
                logger);

            await Should.ThrowAsync<OperationCanceledException>(() =>
                pipeline.ExecuteAsync(new EmptyTriggerContext(DateTimeOffset.UtcNow), executionCts.Token));

            var snapshot = logger.Collector.GetSnapshot();
            snapshot.ShouldNotContain(log => log.Level == LogLevel.Error);

            var cancelled = snapshot.Where(log => log.Message == "Worker pipeline execution cancelled").ShouldHaveSingleItem();
            cancelled.Level.ShouldBe(LogLevel.Debug);
        }

        private static TelemetryWorkerPipeline<EmptyTriggerContext> Create(
            INamedWorkerPipeline<EmptyTriggerContext> inner,
            ILogger? logger = null,
            string trigger = "FakeTrigger",
            Guid? serviceId = null,
            string workerVersion = "1.0.0.0",
            string triggerVersion = "1.0.0.0") =>
            new(inner, logger ?? NullLogger.Instance, serviceId ?? Guid.NewGuid(), workerVersion, trigger, triggerVersion);

        // The ActivitySource is process-wide, so isolate each test by a unique trigger tag.
        private static string NewMarker() => Guid.NewGuid().ToString();

        private static List<Activity> Mine(IEnumerable<Activity> activities, string trigger) =>
            activities.Where(a => Equals(a.GetTagItem("workr.trigger"), trigger)).ToList();

        private static ActivityListener Listen(List<Activity> activities)
        {
            var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "WorkR",
                Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activities.Add
            };

            ActivitySource.AddActivityListener(listener);

            return listener;
        }
    }
}
