using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;

namespace WorkR.Tests
{
    [Trait("Category", "L0")]
    public class WorkerServiceTelemetryTests
    {
        [Fact]
        public async Task ExecuteAsync_WhenListenerSubscribed_CreatesActivityPerExecution()
        {
            var activities = new List<Activity>();
            using var listener = CreateListener(activities);

            var first = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            var second = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            var service = Create(new FakeTrigger(async (next, ct) =>
            {
                await next.ExecuteAsync(first, ct);
                await next.ExecuteAsync(second, ct);
            }));

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            // Other test classes may run pipelines concurrently; only ours carry the FakeTrigger tag
            var mine = activities.Where(a => Equals(a.GetTagItem("workr.trigger"), nameof(FakeTrigger))).ToList();
            mine.Count.ShouldBe(2);
            mine.ShouldAllBe(a => a.OperationName == "EXECUTE FakeWorker");
            mine[0].GetTagItem("workr.execution.id").ShouldBe(first.ExecutionId);
            mine[1].GetTagItem("workr.execution.id").ShouldBe(second.ExecutionId);
            mine.Select(a => a.GetTagItem("workr.service.id")).Distinct().ShouldHaveSingleItem().ShouldBeOfType<Guid>();
            mine.ShouldAllBe(a => Equals(a.GetTagItem("workr.trigger.version"), typeof(FakeTrigger).Assembly.GetName().Version!.ToString()));
            mine.ShouldAllBe(a => Equals(a.GetTagItem("workr.version"), typeof(WorkerService<,>).Assembly.GetName().Version!.ToString()));
            mine.ShouldAllBe(a => Equals(a.GetTagItem("workr.pipeline"), "FakeWorker"));
            mine.ShouldAllBe(a => a.Source.Version == typeof(WorkerService<,>).Assembly.GetName().Version!.ToString());
        }

        [Fact]
        public async Task ExecuteAsync_ActivityNameIsExecuteFollowedByPipeline()
        {
            var activities = new List<Activity>();
            using var listener = CreateListener(activities);

            var context = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            var service = Create(
                new FakeTrigger((next, ct) => next.ExecuteAsync(context, ct)),
                pipeline: new WorkerPipelineBuilder<EmptyTriggerContext>(
                    ["FakeWorker", "OtherFakeWorker"],
                    (_, _, _) => Task.CompletedTask));

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            var activity = activities
                .Where(a => Equals(a.GetTagItem("workr.trigger"), nameof(FakeTrigger)))
                .ShouldHaveSingleItem();
            activity.OperationName.ShouldBe("EXECUTE FakeWorker -> OtherFakeWorker");
            activity.GetTagItem("workr.pipeline").ShouldBe("FakeWorker -> OtherFakeWorker");
        }

        [Fact]
        public async Task ExecuteAsync_WhenWorkerTypeIsGeneric_PrettyPrintsPipelineName()
        {
            var activities = new List<Activity>();
            using var listener = CreateListener(activities);

            var context = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            var service = Create(
                new FakeTrigger((next, ct) => next.ExecuteAsync(context, ct)),
                pipeline: new WorkerPipelineBuilder<EmptyTriggerContext>(
                    ["GenericFakeWorker<string>"],
                    (_, _, _) => Task.CompletedTask));

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            var activity = activities
                .Where(a => Equals(a.GetTagItem("workr.trigger"), nameof(FakeTrigger)))
                .ShouldHaveSingleItem();
            activity.GetTagItem("workr.pipeline").ShouldBe("GenericFakeWorker<string>");
            activity.OperationName.ShouldBe("EXECUTE GenericFakeWorker<string>");
        }

        [Fact]
        public async Task ExecuteAsync_WhenWorkerThrows_MarksActivityAsErrorAndPropagates()
        {
            var activities = new List<Activity>();
            using var listener = CreateListener(activities);

            Exception? caught = null;
            var context = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            var service = Create(
                new FakeTrigger(async (next, ct) =>
                {
                    try
                    {
                        await next.ExecuteAsync(context, ct);
                    }
                    catch (Exception ex)
                    {
                        caught = ex;
                    }
                }),
                pipeline: new WorkerPipelineBuilder<EmptyTriggerContext>(
                    ["FakeWorker"],
                    (_, _, _) => throw new InvalidOperationException("boom")));

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            caught.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("boom");

            var activity = activities
                .Where(a => Equals(a.GetTagItem("workr.trigger"), nameof(FakeTrigger)))
                .ShouldHaveSingleItem();
            activity.Status.ShouldBe(ActivityStatusCode.Error);
            activity.StatusDescription.ShouldBe("boom");
            activity.GetTagItem("error.type").ShouldBe(typeof(InvalidOperationException).FullName);
            activity.Events.ShouldContain(e => e.Name == "exception");
        }

        [Fact]
        public async Task ExecuteAsync_WhenExecutionCancelled_DoesNotMarkActivityAsError()
        {
            var activities = new List<Activity>();
            using var listener = CreateListener(activities);

            var context = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            using var executionCts = new CancellationTokenSource();
            var service = Create(
                new FakeTrigger(async (next, _) =>
                {
                    try
                    {
                        await next.ExecuteAsync(context, executionCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }),
                pipeline: new WorkerPipelineBuilder<EmptyTriggerContext>(
                    ["FakeWorker"],
                    (_, _, ct) =>
                    {
                        executionCts.Cancel();
                        ct.ThrowIfCancellationRequested();
                        return Task.CompletedTask;
                    }));

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            var activity = activities
                .Where(a => Equals(a.GetTagItem("workr.execution.id"), context.ExecutionId))
                .ShouldHaveSingleItem();
            activity.Status.ShouldBe(ActivityStatusCode.Unset);
            activity.GetTagItem("error.type").ShouldBeNull();
            activity.Events.ShouldNotContain(e => e.Name == "exception");
        }

        [Fact]
        public async Task ExecuteAsync_WhenNoListenerSubscribed_DoesNotCreateActivity()
        {
            Activity? observed = null;
            var executed = false;
            var context = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            var service = Create(
                new FakeTrigger((next, ct) => next.ExecuteAsync(context, ct)),
                pipeline: new WorkerPipelineBuilder<EmptyTriggerContext>(
                    ["FakeWorker"],
                    (_, _, _) =>
                    {
                        observed = Activity.Current;
                        executed = true;
                        return Task.CompletedTask;
                    }));

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            executed.ShouldBeTrue();
            observed.ShouldBeNull();
        }

        [Fact]
        public async Task ExecuteAsync_LogsWorkerExecutingAndExecuted_WithExecutionIdScope()
        {
            var provider = new FakeLoggerProvider();
            var context = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            var service = Create(new FakeTrigger((next, ct) => next.ExecuteAsync(context, ct)), loggerProvider: provider);

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            var snapshot = provider.Collector.GetSnapshot();

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
        public async Task ExecuteAsync_BeginsServiceScopeWithVersionTriggerAndPipeline()
        {
            var provider = new FakeLoggerProvider();
            var context = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            var service = Create(new FakeTrigger((next, ct) => next.ExecuteAsync(context, ct)), loggerProvider: provider);

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

        [Fact]
        public async Task ExecuteAsync_WhenWorkerThrows_LogsError()
        {
            var provider = new FakeLoggerProvider();
            var context = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            var service = Create(
                new FakeTrigger(async (next, ct) =>
                {
                    try
                    {
                        await next.ExecuteAsync(context, ct);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }),
                pipeline: new WorkerPipelineBuilder<EmptyTriggerContext>(
                    ["FakeWorker"],
                    (_, _, _) => throw new InvalidOperationException("boom")),
                    loggerProvider: provider);

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            var error = provider.Collector.GetSnapshot().Where(log => log.Level == LogLevel.Error).ShouldHaveSingleItem();
            error.Message.ShouldBe("Worker pipeline execution failed");
            error.Exception.ShouldBeOfType<InvalidOperationException>();
        }

        [Fact]
        public async Task ExecuteAsync_WhenExecutionCancelled_DoesNotLogError()
        {
            var provider = new FakeLoggerProvider();
            var context = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            using var executionCts = new CancellationTokenSource();
            var service = Create(
                new FakeTrigger(async (next, _) =>
                {
                    try
                    {
                        await next.ExecuteAsync(context, executionCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }),
                pipeline: new WorkerPipelineBuilder<EmptyTriggerContext>(
                    ["FakeWorker"],
                    (_, _, ct) =>
                    {
                        executionCts.Cancel();
                        ct.ThrowIfCancellationRequested();
                        return Task.CompletedTask;
                    }),
                    loggerProvider: provider);

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            var snapshot = provider.Collector.GetSnapshot();
            snapshot.ShouldNotContain(log => log.Level == LogLevel.Error);

            var cancelled = snapshot.Where(log => log.Message == "Worker pipeline execution cancelled").ShouldHaveSingleItem();
            cancelled.Level.ShouldBe(LogLevel.Debug);
        }

        private static ActivityListener CreateListener(List<Activity> activities)
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

        private static WorkerService<FakeTrigger, EmptyTriggerContext> Create(
            FakeTrigger? trigger = null,
            WorkerPipelineBuilder<EmptyTriggerContext>? pipeline = null,
            FakeLoggerProvider? loggerProvider = null) =>
            new(EmptyServiceProvider.Instance,
                trigger ?? new FakeTrigger(),
                pipeline ?? new WorkerPipelineBuilder<EmptyTriggerContext>(["FakeWorker"], (_, _, _) => Task.CompletedTask),
                new LoggerFactory([loggerProvider ?? new FakeLoggerProvider()]));

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

        private sealed class OtherFakeWorker : IWorker<EmptyTriggerContext>
        {
            public Task ExecuteAsync(EmptyTriggerContext source, CancellationToken cancellationToken) =>
                Task.CompletedTask;
        }

        private sealed class GenericFakeWorker<T> : IWorker<EmptyTriggerContext>
        {
            public Task ExecuteAsync(EmptyTriggerContext source, CancellationToken cancellationToken) =>
                Task.CompletedTask;
        }
    }
}
