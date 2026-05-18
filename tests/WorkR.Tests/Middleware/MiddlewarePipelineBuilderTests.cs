using Shouldly;
using WorkR.Middleware;

namespace WorkR.Tests.Middleware
{
    public class MiddlewarePipelineBuilderTests
    {
        [Fact]
        public void UseMiddleware_WithFactory_WhenFactoryIsNull_ThrowsArgumentNullException()
        {
            var builder = new MiddlewarePipelineBuilder();

            Should.Throw<ArgumentNullException>(() =>
                builder.UseMiddleware((Func<IServiceProvider, NoopMiddleware>)null!));
        }

        [Fact]
        public void UseMiddleware_WithInstance_WhenMiddlewareIsNull_ThrowsArgumentNullException()
        {
            var builder = new MiddlewarePipelineBuilder();

            Should.Throw<ArgumentNullException>(() =>
                builder.UseMiddleware((IWorkerMiddleware)null!));
        }

        [Fact]
        public void UseMiddleware_ReturnsBuilderForChaining()
        {
            var builder = new MiddlewarePipelineBuilder();

            builder.UseMiddleware(new NoopMiddleware()).ShouldBeSameAs(builder);
        }

        [Fact]
        public void Build_WhenWorkerExecutionIsNull_ThrowsArgumentNullException()
        {
            var builder = new MiddlewarePipelineBuilder();

            Should.Throw<ArgumentNullException>(() => builder.Build(null!));
        }

        [Fact]
        public async Task Build_WhenNoMiddlewareRegistered_CallsWorkerDirectly()
        {
            var called = false;
            var builder = new MiddlewarePipelineBuilder();

            var pipeline = builder.Build((sp, ct) => { called = true; return Task.CompletedTask; });
            await pipeline(null!, TestContext.Current.CancellationToken);

            called.ShouldBeTrue();
        }

        [Fact]
        public async Task Build_ExecutesMiddlewareInRegistrationOrder()
        {
            var order = new List<string>();
            var builder = new MiddlewarePipelineBuilder();
            builder.UseMiddleware(new RecordingMiddleware("A", order));
            builder.UseMiddleware(new RecordingMiddleware("B", order));

            var pipeline = builder.Build((sp, ct) => Task.CompletedTask);
            await pipeline(null!, TestContext.Current.CancellationToken);

            order.ShouldBe(["A", "B"]);
        }

        [Fact]
        public async Task Build_MiddlewareCanShortCircuitPipeline()
        {
            var workerCalled = false;
            var builder = new MiddlewarePipelineBuilder();
            builder.UseMiddleware(new ShortCircuitMiddleware());

            var pipeline = builder.Build((sp, ct) => { workerCalled = true; return Task.CompletedTask; });
            await pipeline(null!, TestContext.Current.CancellationToken);

            workerCalled.ShouldBeFalse();
        }

        private sealed class NoopMiddleware : IWorkerMiddleware
        {
            public Task Execute(Func<CancellationToken, Task> next, CancellationToken ct) => next(ct);
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

            public async Task Execute(Func<CancellationToken, Task> next, CancellationToken ct)
            {
                _order.Add(_name);
                await next(ct);
            }
        }

        private sealed class ShortCircuitMiddleware : IWorkerMiddleware
        {
            public Task Execute(Func<CancellationToken, Task> next, CancellationToken ct) =>
                Task.CompletedTask;
        }
    }
}
