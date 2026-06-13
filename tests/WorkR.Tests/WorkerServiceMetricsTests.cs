using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;

namespace WorkR.Tests
{
    [Trait("Category", "L0")]
    public class WorkerServiceMetricsTests
    {
        [Fact]
        public async Task ExecuteAsync_WhenCollectorSubscribed_RecordsDurationPerExecution()
        {
            using var collector = CreateCollector();

            var first = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            var second = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            var service = Create(new MetricsFakeTrigger(async (next, ct) =>
            {
                await next(first, ct);
                await next(second, ct);
            }));

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            var mine = Mine(collector);
            mine.Count.ShouldBe(2);
            mine.ShouldAllBe(m => m.Value >= 0);
            mine.ShouldAllBe(m => Equals(m.Tags["workr.pipeline"], "MetricsFakeWorker"));
            mine.ShouldAllBe(m => !m.Tags.ContainsKey("error.type"));
        }

        [Fact]
        public void Instrument_IsSecondsHistogramOnTheWorkRMeter()
        {
            using var collector = CreateCollector();

            var instrument = collector.Instrument.ShouldNotBeNull();
            instrument.Name.ShouldBe("workr.execution.duration");
            instrument.Unit.ShouldBe("s");
            instrument.Meter.Name.ShouldBe("WorkR");
        }

        [Fact]
        public async Task ExecuteAsync_WhenWorkerThrows_RecordsDurationTaggedWithErrorType()
        {
            using var collector = CreateCollector();

            var context = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            var service = Create(
                new MetricsFakeTrigger(async (next, ct) =>
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
                    (_, _, _) => throw new InvalidOperationException("boom"),
                    [typeof(MetricsFakeWorker)]));

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            var measurement = Mine(collector).ShouldHaveSingleItem();
            measurement.Tags["error.type"].ShouldBe(typeof(InvalidOperationException).FullName);
        }

        [Fact]
        public async Task ExecuteAsync_WhenExecutionCancelled_DoesNotRecordDuration()
        {
            using var collector = CreateCollector();

            var context = new EmptyTriggerContext(DateTimeOffset.UtcNow);
            using var executionCts = new CancellationTokenSource();
            var service = Create(
                new MetricsFakeTrigger(async (next, _) =>
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
                }, [typeof(MetricsFakeWorker)]));

            await service.StartAsync(TestContext.Current.CancellationToken);
            await service.ExecuteTask!;

            Mine(collector).ShouldBeEmpty();
        }

        private static MetricCollector<double> CreateCollector() =>
            new(WorkRDiagnostics.Meter, "workr.execution.duration");

        // Other test classes may run pipelines concurrently against the shared
        // static meter; only ours carry the MetricsFakeTrigger tag.
        private static List<CollectedMeasurement<double>> Mine(MetricCollector<double> collector) =>
            collector.GetMeasurementSnapshot()
                .Where(m => Equals(m.Tags.GetValueOrDefault("workr.trigger"), nameof(MetricsFakeTrigger)))
                .ToList();

        private static WorkerService<MetricsFakeTrigger, EmptyTriggerContext> Create(
            MetricsFakeTrigger? trigger = null,
            WorkerPipeline<EmptyTriggerContext>? pipeline = null) =>
            new(EmptyServiceProvider.Instance,
                trigger ?? new MetricsFakeTrigger(),
                pipeline ?? new WorkerPipeline<EmptyTriggerContext>((_, _, _) => Task.CompletedTask, [typeof(MetricsFakeWorker)]),
                new Microsoft.Extensions.Logging.Testing.FakeLogger<WorkerService<MetricsFakeTrigger, EmptyTriggerContext>>());

        private sealed class EmptyServiceProvider : IServiceProvider
        {
            public static readonly EmptyServiceProvider Instance = new();

            public object? GetService(Type serviceType) => null;
        }

        private sealed class MetricsFakeTrigger : ITrigger<EmptyTriggerContext>
        {
            private readonly Func<WorkerDelegate<EmptyTriggerContext>, CancellationToken, Task> _execute;

            public MetricsFakeTrigger(
                Func<WorkerDelegate<EmptyTriggerContext>, CancellationToken, Task>? execute = null)
            {
                _execute = execute ?? ((_, _) => Task.CompletedTask);
            }

            public Task ExecuteAsync(WorkerDelegate<EmptyTriggerContext> workerPipeline, CancellationToken stoppingToken) =>
                _execute(workerPipeline, stoppingToken);
        }

        private sealed class MetricsFakeWorker : IWorker<EmptyTriggerContext>
        {
            public Task ExecuteAsync(EmptyTriggerContext source, CancellationToken cancellationToken) =>
                Task.CompletedTask;
        }
    }
}
