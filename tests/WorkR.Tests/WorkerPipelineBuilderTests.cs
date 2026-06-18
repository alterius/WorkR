using System.Text.RegularExpressions;
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
        public void WorkerPipelineFinal_Name_JoinsProvidedNames()
        {
            using var sp = new ServiceCollection().BuildServiceProvider();
            var pipeline = new WorkerPipelineBuilder<string>(
                ["UpperCaseWorker", "CapturingWorker"],
                (_, _, _) => Task.CompletedTask);

            pipeline.Build(sp).Name.ShouldBe("UpperCaseWorker -> CapturingWorker");
        }

        [Fact]
        public async Task Build_ReturnsDelegate_ThatInvokesPipeline()
        {
            var called = false;
            await using var sp = new ServiceCollection().BuildServiceProvider();
            var pipeline = new WorkerPipelineBuilder<string>(
                ["CapturingWorker"],
                (sp, value, ct) =>
                {
                    called = true;
                    return Task.CompletedTask;
                });

            await pipeline.Build(sp).ExecuteAsync("hello", TestContext.Current.CancellationToken);

            called.ShouldBeTrue();
        }

        [Fact]
        public async Task Build_CreatesNewScopePerExecution()
        {
            var captured = new List<IServiceProvider>();
            await using var sp = new ServiceCollection().BuildServiceProvider();
            var pipeline = new WorkerPipelineBuilder<string>(
                ["CapturingWorker"],
                (serviceProvider, _, _) =>
                {
                    captured.Add(serviceProvider);
                    return Task.CompletedTask;
                });

            var del = pipeline.Build(sp);
            await del.ExecuteAsync("hello", TestContext.Current.CancellationToken);
            await del.ExecuteAsync("hello", TestContext.Current.CancellationToken);

            // Each execution runs in its own scope, distinct from the root provider and each other.
            captured.Count.ShouldBe(2);
            captured[0].ShouldNotBeSameAs(sp);
            captured[1].ShouldNotBeSameAs(sp);
            captured[0].ShouldNotBeSameAs(captured[1]);
        }

        [Fact]
        public async Task Build_PassesValueToPipeline()
        {
            string? captured = null;
            await using var sp = new ServiceCollection().BuildServiceProvider();
            var pipeline = new WorkerPipelineBuilder<string>(
                ["CapturingWorker"],
                (_, value, _) =>
                {
                    captured = value;
                    return Task.CompletedTask;
                });

            await pipeline.Build(sp).ExecuteAsync("hello", TestContext.Current.CancellationToken);

            captured.ShouldBe("hello");
        }

        [Fact]
        public async Task Build_WithNullSerivceProvider_ThrowsException()
        {
            var pipeline = new WorkerPipelineBuilder<string>(
                ["ThrowingWorker"],
                (_, value, _) => Task.FromException(new Exception()));

            Should.Throw<ArgumentNullException>(() => pipeline.Build(null!));
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
            await using var sp = new ServiceCollection().BuildServiceProvider();

            var del = WorkerPipelineBuilder.Create<string>()
                .Finally("capture", (sp, value, ct) =>
                {
                    captured = value;
                    return Task.CompletedTask;
                })
                .Build(sp);

            await del.ExecuteAsync("hello", TestContext.Current.CancellationToken);

            captured.ShouldBe("hello");
        }

        [Fact]
        public async Task Then_TransformsValueBeforeNextStage()
        {
            string? captured = null;
            await using var sp = new ServiceCollection().BuildServiceProvider();

            var del = WorkerPipelineBuilder.Create<string>()
                .Then<string>("upper", (sp, value, next, ct) => next(sp, value.ToUpper(), ct))
                .Finally("capture", (sp, value, ct) =>
                {
                    captured = value;
                    return Task.CompletedTask;
                })
                .Build(sp);

            await del.ExecuteAsync("hello", TestContext.Current.CancellationToken);

            captured.ShouldBe("HELLO");
        }

        [Fact]
        public async Task Finally_CallsTerminalStepWithValue()
        {
            string? captured = null;
            await using var sp = new ServiceCollection().BuildServiceProvider();

            var del = WorkerPipelineBuilder.Create<string>()
                .Finally("capture", (sp, value, ct) =>
                {
                    captured = value;
                    return Task.CompletedTask;
                })
                .Build(sp);

            await del.ExecuteAsync("world", TestContext.Current.CancellationToken);

            captured.ShouldBe("world");
        }

        [Fact]
        public void Compose_RecordsWorkerNamesInPipelineOrder()
        {
            using var sp = new ServiceCollection().BuildServiceProvider();
            var pipeline = WorkerPipelineBuilder.Create<string>()
                .Then<string>("upper", (sp, value, next, ct) => next(sp, value, ct))
                .Finally("capture", (sp, value, ct) => Task.CompletedTask);

            pipeline.Build(sp).Name.ShouldBe("upper -> capture");
        }

        [Fact]
        public async Task Then_AppliesMiddlewareAroundStep()
        {
            var middlewareCalled = false;
            await using var sp = new ServiceCollection().BuildServiceProvider();

            var del = WorkerPipelineBuilder.Create<string>()
                .Then<string>("upper", (sp, value, next, ct) => next(sp, value, ct),
                    mw => mw.UseMiddleware(new CallbackMiddleware(() => middlewareCalled = true)))
                .Finally("capture", (sp, value, ct) => Task.CompletedTask)
                .Build(sp);

            await del.ExecuteAsync("hello", TestContext.Current.CancellationToken);

            middlewareCalled.ShouldBeTrue();
        }

        [Fact]
        public async Task Finally_AppliesMiddlewareAroundStep()
        {
            var middlewareCalled = false;
            await using var sp = new ServiceCollection().BuildServiceProvider();

            var del = WorkerPipelineBuilder.Create<string>()
                .Finally("capture", (sp, value, ct) => Task.CompletedTask,
                    mw => mw.UseMiddleware(new CallbackMiddleware(() => middlewareCalled = true)))
                .Build(sp);

            await del.ExecuteAsync("hello", TestContext.Current.CancellationToken);

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
