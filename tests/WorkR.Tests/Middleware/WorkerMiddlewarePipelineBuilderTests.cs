using Shouldly;
using WorkR.Middleware;

namespace WorkR.Tests.Middleware
{
    [Trait("Category", "L0")]
    public class WorkerMiddlewarePipelineBuilderTests
    {
        [Fact]
        public void UseMiddleware_WithFactory_WhenFactoryIsNull_ThrowsArgumentNullException()
        {
            var builder = new WorkerMiddlewarePipelineBuilder();

            Should.Throw<ArgumentNullException>(() =>
                builder.UseMiddleware((Func<IServiceProvider, NoopMiddleware>)null!));
        }

        [Fact]
        public void UseMiddleware_WithInstance_WhenMiddlewareIsNull_ThrowsArgumentNullException()
        {
            var builder = new WorkerMiddlewarePipelineBuilder();

            Should.Throw<ArgumentNullException>(() =>
                builder.UseMiddleware((IWorkerMiddleware)null!));
        }

        [Fact]
        public void UseMiddleware_ReturnsBuilderForChaining()
        {
            var builder = new WorkerMiddlewarePipelineBuilder();

            builder.UseMiddleware(new NoopMiddleware()).ShouldBeSameAs(builder);
        }

        [Fact]
        public async Task Build_WhenNoMiddlewareRegistered_CallsWorkerDirectly()
        {
            var called = false;
            var builder = new WorkerMiddlewarePipelineBuilder();

            await builder.Build()(null!, (sp, ct) => { called = true; return Task.CompletedTask; }, TestContext.Current.CancellationToken);

            called.ShouldBeTrue();
        }

        [Fact]
        public async Task Build_ExecutesMiddlewareInRegistrationOrder()
        {
            var order = new List<string>();
            var builder = new WorkerMiddlewarePipelineBuilder();
            builder.UseMiddleware(new RecordingMiddleware("A", order));
            builder.UseMiddleware(new RecordingMiddleware("B", order));

            await builder.Build()(null!, (sp, ct) => Task.CompletedTask, TestContext.Current.CancellationToken);

            order.ShouldBe(["A", "B"]);
        }

        [Fact]
        public async Task Build_MiddlewareCanShortCircuitPipeline()
        {
            var workerCalled = false;
            var builder = new WorkerMiddlewarePipelineBuilder();
            builder.UseMiddleware(new ShortCircuitMiddleware());

            await builder.Build()(null!, (sp, ct) => { workerCalled = true; return Task.CompletedTask; }, TestContext.Current.CancellationToken);

            workerCalled.ShouldBeFalse();
        }

        [Fact]
        public void UseTimeout_ReturnsBuilderForChaining()
        {
            var builder = new WorkerMiddlewarePipelineBuilder();

            builder.UseTimeout(TimeSpan.FromSeconds(1)).ShouldBeSameAs(builder);
        }

        [Fact]
        public void UseInternalMiddleware_WithFactory_WhenFactoryIsNull_ThrowsArgumentNullException()
        {
            var builder = new WorkerMiddlewarePipelineBuilder();

            Should.Throw<ArgumentNullException>(() =>
                builder.UseInternalMiddleware((Func<IServiceProvider, ScopeMiddleware>)null!));
        }

        [Fact]
        public void UseInternalMiddleware_WithFactory_ReturnsBuilderForChaining()
        {
            var builder = new WorkerMiddlewarePipelineBuilder();

            builder.UseInternalMiddleware(_ => new ScopeMiddleware()).ShouldBeSameAs(builder);
        }

        private sealed class NoopMiddleware : IWorkerMiddleware
        {
            public Task ExecuteAsync(Func<CancellationToken, Task> next, CancellationToken cancellationToken) => next(cancellationToken);
        }

        private sealed class RecordingMiddleware : IWorkerMiddleware
        {
            private readonly string _name;
            private readonly List<string> _order;

            public RecordingMiddleware(string name, List<string> order)
            {
                _name = name;
                _order = order;
            }

            public async Task ExecuteAsync(Func<CancellationToken, Task> next, CancellationToken cancellationToken)
            {
                _order.Add(_name);
                await next(cancellationToken);
            }
        }

        private sealed class ShortCircuitMiddleware : IWorkerMiddleware
        {
            public Task ExecuteAsync(Func<CancellationToken, Task> next, CancellationToken cancellationToken) =>
                Task.CompletedTask;
        }
    }
}
