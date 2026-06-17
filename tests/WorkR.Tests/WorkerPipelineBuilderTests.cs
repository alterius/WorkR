using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace WorkR.Tests
{
    [Trait("Category", "L0")]
    public class WorkerPipelineBuilderTests
    {
        // WorkerPipelineBuilder<TIn> — public class, internal constructor

        [Fact]
        public void WorkerPipelineFinal_Constructor_WhenPipelineIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new WorkerPipelineBuilder<string>([], null!));
        }

        [Fact]
        public void WorkerPipelineFinal_Constructor_WhenWorkerNamesIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new WorkerPipelineBuilder<string>(null!, (_, _, _) => Task.CompletedTask));
        }

        [Fact]
        public void WorkerPipelineFinal_Constructor_WhenWorkerNamesIsEmpty_ThrowsArgumentException()
        {
            Should.Throw<ArgumentException>(() =>
                new WorkerPipelineBuilder<string>([], (_, _, _) => Task.CompletedTask));
        }

        [Fact]
        public void WorkerPipelineFinal_WorkerNames_ExposesProvidedNames()
        {
            var pipeline = new WorkerPipelineBuilder<string>(
                ["UpperCaseWorker", "CapturingWorker"],
                (_, _, _) => Task.CompletedTask);

            pipeline.WorkerNames.ShouldBe(["UpperCaseWorker", "CapturingWorker"]);
        }

        [Fact]
        public async Task Build_ReturnsDelegate_ThatInvokesPipeline()
        {
            var called = false;
            var pipeline = new WorkerPipelineBuilder<string>(
                ["CapturingWorker"],
                (sp, value, ct) =>
                {
                    called = true;
                    return Task.CompletedTask;
                });

            await pipeline.Build(null!).Invoke("hello", TestContext.Current.CancellationToken);

            called.ShouldBeTrue();
        }

        [Fact]
        public async Task Build_PassesServiceProviderToPipeline()
        {
            IServiceProvider? captured = null;
            await using var sp = new ServiceCollection().BuildServiceProvider();
            var pipeline = new WorkerPipelineBuilder<string>(
                ["CapturingWorker"],
                (serviceProvider, _, _) =>
                {
                    captured = serviceProvider;
                    return Task.CompletedTask;
                });

            await pipeline.Build(sp).Invoke("hello", TestContext.Current.CancellationToken);

            captured.ShouldBeSameAs(sp);
        }

        [Fact]
        public async Task Build_PassesValueToPipeline()
        {
            string? captured = null;
            var pipeline = new WorkerPipelineBuilder<string>(
                ["CapturingWorker"],
                (_, value, _) =>
                {
                    captured = value;
                    return Task.CompletedTask;
                });

            await pipeline.Build(null!).Invoke("hello", TestContext.Current.CancellationToken);

            captured.ShouldBe("hello");
        }

        // WorkerPipelineBuilder<TIn, TOut> — internal class, internal constructor

        [Fact]
        public void WorkerPipelineIntermediate_Constructor_WhenPipelineIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new WorkerPipelineBuilder<string, string>([], null!));
        }

        // WorkerPipelineBuilder.Create / Then / Finally — pipeline composition from step delegates

        [Fact]
        public async Task Create_ProducesPassthroughPipeline()
        {
            string? captured = null;

            var del = WorkerPipelineBuilder.Create<string>()
                .Finally("capture", (sp, value, ct) =>
                {
                    captured = value;
                    return Task.CompletedTask;
                })
                .Build(null!);

            await del("hello", TestContext.Current.CancellationToken);

            captured.ShouldBe("hello");
        }

        [Fact]
        public async Task Then_TransformsValueBeforeNextStage()
        {
            string? captured = null;

            var del = WorkerPipelineBuilder.Create<string>()
                .Then<string>("upper", (sp, value, next, ct) => next(sp, value.ToUpper(), ct))
                .Finally("capture", (sp, value, ct) =>
                {
                    captured = value;
                    return Task.CompletedTask;
                })
                .Build(null!);

            await del("hello", TestContext.Current.CancellationToken);

            captured.ShouldBe("HELLO");
        }

        [Fact]
        public async Task Finally_CallsTerminalStepWithValue()
        {
            string? captured = null;

            var del = WorkerPipelineBuilder.Create<string>()
                .Finally("capture", (sp, value, ct) =>
                {
                    captured = value;
                    return Task.CompletedTask;
                })
                .Build(null!);

            await del("world", TestContext.Current.CancellationToken);

            captured.ShouldBe("world");
        }

        [Fact]
        public void Compose_RecordsWorkerNamesInPipelineOrder()
        {
            var pipeline = WorkerPipelineBuilder.Create<string>()
                .Then<string>("upper", (sp, value, next, ct) => next(sp, value, ct))
                .Finally("capture", (sp, value, ct) => Task.CompletedTask);

            pipeline.WorkerNames.ShouldBe(["upper", "capture"]);
        }

        [Fact]
        public async Task Then_AppliesMiddlewareAroundStep()
        {
            var middlewareCalled = false;

            var del = WorkerPipelineBuilder.Create<string>()
                .Then<string>("upper", (sp, value, next, ct) => next(sp, value, ct),
                    mw => mw.UseMiddleware(new CallbackMiddleware(() => middlewareCalled = true)))
                .Finally("capture", (sp, value, ct) => Task.CompletedTask)
                .Build(null!);

            await del("hello", TestContext.Current.CancellationToken);

            middlewareCalled.ShouldBeTrue();
        }

        [Fact]
        public async Task Finally_AppliesMiddlewareAroundStep()
        {
            var middlewareCalled = false;

            var del = WorkerPipelineBuilder.Create<string>()
                .Finally("capture", (sp, value, ct) => Task.CompletedTask,
                    mw => mw.UseMiddleware(new CallbackMiddleware(() => middlewareCalled = true)))
                .Build(null!);

            await del("hello", TestContext.Current.CancellationToken);

            middlewareCalled.ShouldBeTrue();
        }

        private sealed class CallbackMiddleware : IWorkerMiddleware
        {
            private readonly Action _onExecute;

            public CallbackMiddleware(Action onExecute) => _onExecute = onExecute;

            public async Task ExecuteAsync(Func<CancellationToken, Task> next, CancellationToken cancellationToken)
            {
                _onExecute();
                await next(cancellationToken);
            }
        }
    }
}
