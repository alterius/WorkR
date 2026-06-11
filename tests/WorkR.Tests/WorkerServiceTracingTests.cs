using System.Diagnostics;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;

namespace WorkR.Tests
{
    [Trait("Category", "L0")]
    public class WorkerServiceTracingTests
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
            activity.Events.ShouldContain(e => e.Name == "exception");
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
            WorkerPipeline<EmptyTriggerContext>? pipeline = null) =>
            new(EmptyServiceProvider.Instance,
                trigger ?? new FakeTrigger(),
                pipeline ?? new WorkerPipeline<EmptyTriggerContext>((_, _, _) => Task.CompletedTask),
                new FakeLogger<WorkerService<FakeTrigger, EmptyTriggerContext>>());

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
