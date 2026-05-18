using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace WorkR.Tests
{
    [Trait("Category", "L0")]
    public class WorkerPipelineTests
    {
        // WorkerPipeline<TIn> — public class, internal constructor

        [Fact]
        public void WorkerPipelineFinal_Constructor_WhenPipelineIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new WorkerPipeline<string>((WorkerPipelineDelegate<string>)null!));
        }

        [Fact]
        public async Task Build_ReturnsDelegate_ThatInvokesPipeline()
        {
            var called = false;
            var pipeline = new WorkerPipeline<string>((sp, value, ct) =>
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
            var pipeline = new WorkerPipeline<string>((serviceProvider, _, _) =>
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
            var pipeline = new WorkerPipeline<string>((_, value, _) =>
            {
                captured = value;
                return Task.CompletedTask;
            });

            await pipeline.Build(null!).Invoke("hello", TestContext.Current.CancellationToken);

            captured.ShouldBe("hello");
        }

        // WorkerPipeline<TIn, TOut> — internal class, internal constructor

        [Fact]
        public void WorkerPipelineIntermediate_Constructor_WhenPipelineIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new WorkerPipeline<string, string>((WorkerPipelineDelegate<string, string>)null!));
        }

        // WorkerPipeline.Create / Then / Finally — pipeline composition

        [Fact]
        public async Task Create_ProducesPassthroughPipeline()
        {
            await using var sp = new ServiceCollection()
                .AddSingleton<CapturingWorker>()
                .BuildServiceProvider();

            var del = WorkerPipeline.Create<string>()
                .Finally<CapturingWorker>()
                .Build(sp);

            await del("hello", TestContext.Current.CancellationToken);

            sp.GetRequiredService<CapturingWorker>().Captured.ShouldBe("hello");
        }

        [Fact]
        public async Task Then_TransformsValueBeforeNextStage()
        {
            await using var sp = new ServiceCollection()
                .AddTransient<UpperCaseWorker>()
                .AddSingleton<CapturingWorker>()
                .BuildServiceProvider();

            var del = WorkerPipeline.Create<string>()
                .Then<UpperCaseWorker, string>()
                .Finally<CapturingWorker>()
                .Build(sp);

            await del("hello", TestContext.Current.CancellationToken);

            sp.GetRequiredService<CapturingWorker>().Captured.ShouldBe("HELLO");
        }

        [Fact]
        public async Task Finally_CallsTerminalWorkerWithValue()
        {
            await using var sp = new ServiceCollection()
                .AddTransient<UpperCaseWorker>()
                .AddSingleton<CapturingWorker>()
                .BuildServiceProvider();

            var del = WorkerPipeline.Create<string>()
                .Then<UpperCaseWorker, string>()
                .Finally<CapturingWorker>()
                .Build(sp);

            await del("world", TestContext.Current.CancellationToken);

            sp.GetRequiredService<CapturingWorker>().Captured.ShouldBe("WORLD");
        }

        [Fact]
        public async Task Then_AppliesMiddlewareAroundWorker()
        {
            var middlewareCalled = false;
            await using var sp = new ServiceCollection()
                .AddTransient<UpperCaseWorker>()
                .AddSingleton<CapturingWorker>()
                .BuildServiceProvider();

            var del = WorkerPipeline.Create<string>()
                .Then<UpperCaseWorker, string>(mw =>
                    mw.UseMiddleware(new CallbackMiddleware(() => middlewareCalled = true)))
                .Finally<CapturingWorker>()
                .Build(sp);

            await del("hello", TestContext.Current.CancellationToken);

            middlewareCalled.ShouldBeTrue();
        }

        private sealed class UpperCaseWorker : IWorker<string, string>
        {
            public Task Execute(string source, WorkerDelegate<string> next, CancellationToken ct) =>
                next(source.ToUpper(), ct);
        }

        private sealed class CapturingWorker : IWorker<string>
        {
            public string? Captured { get; private set; }

            public Task Execute(string source, CancellationToken ct)
            {
                Captured = source;
                return Task.CompletedTask;
            }
        }

        private sealed class CallbackMiddleware : IWorkerMiddleware
        {
            private readonly Action _onExecute;

            public CallbackMiddleware(Action onExecute) => _onExecute = onExecute;

            public async Task Execute(Func<CancellationToken, Task> next, CancellationToken ct)
            {
                _onExecute();
                await next(ct);
            }
        }
    }
}
