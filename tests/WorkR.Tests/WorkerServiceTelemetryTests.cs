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
                await next(first, ct);
                await next(second, ct);
            }));

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            // Other test classes may run pipelines concurrently; only ours carry the FakeTrigger tag
            var mine = activities.Where(a => Equals(a.GetTagItem("workr.trigger"), nameof(FakeTrigger))).ToList();
            mine.Count.ShouldBe(2);
            mine.ShouldAllBe(a => a.OperationName == nameof(EmptyTriggerContext));
            mine[0].GetTagItem("workr.execution.id").ShouldBe(first.ExecutionId);
            mine[1].GetTagItem("workr.execution.id").ShouldBe(second.ExecutionId);
            mine.Select(a => a.GetTagItem("workr.service.id")).Distinct().ShouldHaveSingleItem().ShouldBeOfType<Guid>();
            mine.ShouldAllBe(a => Equals(a.GetTagItem("workr.trigger.version"), typeof(FakeTrigger).Assembly.GetName().Version!.ToString()));
            mine.ShouldAllBe(a => a.Source.Version == typeof(WorkerService<,>).Assembly.GetName().Version!.ToString());
        }

        [Fact]
        public async Task ExecuteAsync_WhenContextTypeIsGeneric_StripsArityFromActivityName()
        {
            var activities = new List<Activity>();
            using var listener = CreateListener(activities);

            var context = new ValueTriggerContext<string>(DateTimeOffset.UtcNow, "value");
            var service = new WorkerService<FakeValueTrigger, ValueTriggerContext<string>>(
                EmptyServiceProvider.Instance,
                new FakeValueTrigger(context),
                new WorkerPipeline<ValueTriggerContext<string>>((_, _, _) => Task.CompletedTask),
                new FakeLogger<WorkerService<FakeValueTrigger, ValueTriggerContext<string>>>());

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            var activity = activities
                .Where(a => Equals(a.GetTagItem("workr.trigger"), nameof(FakeValueTrigger)))
                .ShouldHaveSingleItem();
            activity.OperationName.ShouldBe("ValueTriggerContext");
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
                        await next(context, ct);
                    }
                    catch (Exception ex)
                    {
                        caught = ex;
                    }
                }),
                pipeline: new WorkerPipeline<EmptyTriggerContext>(
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
                        await next(context, executionCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }),
                pipeline: new WorkerPipeline<EmptyTriggerContext>((_, _, ct) =>
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
                new FakeTrigger((next, ct) => next(context, ct)),
                pipeline: new WorkerPipeline<EmptyTriggerContext>((_, _, _) =>
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
            var logger = new FakeLogger<WorkerService<FakeTrigger, EmptyTriggerContext>>();
            var context = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            var service = Create(new FakeTrigger((next, ct) => next(context, ct)), logger: logger);

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            var snapshot = logger.Collector.GetSnapshot();

            var executing = snapshot.Where(log => log.Message == "Worker pipeline executing...").ShouldHaveSingleItem();
            executing.Level.ShouldBe(LogLevel.Debug);
            executing.Scopes
                .OfType<IEnumerable<KeyValuePair<string, object?>>>()
                .SelectMany(scope => scope)
                .ShouldContain(new KeyValuePair<string, object?>("ExecutionId", context.ExecutionId));

            var executed = snapshot.Where(log => log.Message.StartsWith("Worker pipeline executed in")).ShouldHaveSingleItem();
            executed.Level.ShouldBe(LogLevel.Debug);
        }

        [Fact]
        public async Task ExecuteAsync_WhenWorkerThrows_LogsError()
        {
            var logger = new FakeLogger<WorkerService<FakeTrigger, EmptyTriggerContext>>();
            var context = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            var service = Create(
                new FakeTrigger(async (next, ct) =>
                {
                    try
                    {
                        await next(context, ct);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }),
                pipeline: new WorkerPipeline<EmptyTriggerContext>(
                    (_, _, _) => throw new InvalidOperationException("boom")),
                logger: logger);

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            var error = logger.Collector.GetSnapshot().Where(log => log.Level == LogLevel.Error).ShouldHaveSingleItem();
            error.Message.ShouldBe("Worker pipeline execution failed");
            error.Exception.ShouldBeOfType<InvalidOperationException>();
        }

        [Fact]
        public async Task ExecuteAsync_WhenExecutionCancelled_DoesNotLogError()
        {
            var logger = new FakeLogger<WorkerService<FakeTrigger, EmptyTriggerContext>>();
            var context = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            using var executionCts = new CancellationTokenSource();
            var service = Create(
                new FakeTrigger(async (next, _) =>
                {
                    try
                    {
                        await next(context, executionCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }),
                pipeline: new WorkerPipeline<EmptyTriggerContext>((_, _, ct) =>
                {
                    executionCts.Cancel();
                    ct.ThrowIfCancellationRequested();
                    return Task.CompletedTask;
                }),
                logger: logger);

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            logger.Collector.GetSnapshot().ShouldNotContain(log => log.Level == LogLevel.Error);
        }

        private static ActivityListener CreateListener(List<Activity> activities)
        {
            var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "WorkR",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activities.Add
            };

            ActivitySource.AddActivityListener(listener);

            return listener;
        }

        private static WorkerService<FakeTrigger, EmptyTriggerContext> Create(
            FakeTrigger? trigger = null,
            WorkerPipeline<EmptyTriggerContext>? pipeline = null,
            FakeLogger<WorkerService<FakeTrigger, EmptyTriggerContext>>? logger = null) =>
            new(EmptyServiceProvider.Instance,
                trigger ?? new FakeTrigger(),
                pipeline ?? new WorkerPipeline<EmptyTriggerContext>((_, _, _) => Task.CompletedTask),
                logger ?? new FakeLogger<WorkerService<FakeTrigger, EmptyTriggerContext>>());

        private sealed class EmptyServiceProvider : IServiceProvider
        {
            public static readonly EmptyServiceProvider Instance = new();

            public object? GetService(Type serviceType) => null;
        }

        private sealed class FakeTrigger : ITrigger<EmptyTriggerContext>
        {
            private readonly Func<WorkerDelegate<EmptyTriggerContext>, CancellationToken, Task> _execute;

            public FakeTrigger(
                Func<WorkerDelegate<EmptyTriggerContext>, CancellationToken, Task>? execute = null)
            {
                _execute = execute ?? ((_, _) => Task.CompletedTask);
            }

            public Task ExecuteAsync(WorkerDelegate<EmptyTriggerContext> workerPipeline, CancellationToken stoppingToken) =>
                _execute(workerPipeline, stoppingToken);
        }

        private sealed class FakeValueTrigger : ITrigger<ValueTriggerContext<string>>
        {
            private readonly ValueTriggerContext<string> _context;

            public FakeValueTrigger(ValueTriggerContext<string> context)
            {
                _context = context;
            }

            public Task ExecuteAsync(WorkerDelegate<ValueTriggerContext<string>> workerPipeline, CancellationToken stoppingToken) =>
                workerPipeline(_context, stoppingToken);
        }
    }
}
