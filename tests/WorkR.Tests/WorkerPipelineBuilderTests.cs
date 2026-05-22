using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WorkR.Middleware;

namespace WorkR.Tests
{
    [Trait("Category", "L0")]
    public class WorkerPipelineBuilderTests
    {
        [Fact]
        public void Constructor_WhenServicesIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new WorkerPipelineBuilder<FakeTrigger, EmptyTriggerContext>(
                    null!, WorkerPipeline.Create<EmptyTriggerContext>()));
        }

        [Fact]
        public void Constructor_WhenBuilderIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new WorkerPipelineBuilder<FakeTrigger, EmptyTriggerContext>(
                    new ServiceCollection(), null!));
        }

        [Fact]
        public void AddWorker_WithNullLifetime_DoesNotRegisterWorkerInServices()
        {
            var services = new ServiceCollection();

            CreateBuilder(services).AddWorker<FakeTerminalWorker>(lifetime: null);

            services.ShouldNotContain(d => d.ServiceType == typeof(FakeTerminalWorker));
        }

        [Fact]
        public void AddWorker_Terminal_WithFactory_RegistersWorkerViaFactory()
        {
            var services = new ServiceCollection();

            CreateBuilder(services).AddWorker(_ => new FakeTerminalWorker());

            services.ShouldContain(d => d.ServiceType == typeof(FakeTerminalWorker));
        }

        [Fact]
        public void AddWorker_Transform_WithFactory_RegistersWorkerViaFactory()
        {
            var services = new ServiceCollection();

            CreateBuilder(services).AddWorker<FakeTransformWorker, string>(_ => new FakeTransformWorker());

            services.ShouldContain(d => d.ServiceType == typeof(FakeTransformWorker));
        }

        [Fact]
        public async Task AddWorker_WithDefaultMiddleware_AppliesItWhenNoExplicitMiddlewareGiven()
        {
            var defaultCalled = false;
            var services = new ServiceCollection().AddSingleton<FakeTerminalWorker>();
            var builder = CreateBuilder(services,
                defaultMiddleware: mw => mw.UseMiddleware(new RecordingMiddleware(() => defaultCalled = true)));

            var pipeline = builder.AddWorker<FakeTerminalWorker>();
            await using var sp = services.BuildServiceProvider();
            await pipeline.Build(sp)(new EmptyTriggerContext(DateTimeOffset.UtcNow), CancellationToken.None);

            defaultCalled.ShouldBeTrue();
        }

        [Fact]
        public async Task AddWorker_WithDefaultAndExplicitMiddleware_AppliesBoth()
        {
            var defaultCalled = false;
            var explicitCalled = false;
            var services = new ServiceCollection().AddSingleton<FakeTerminalWorker>();
            var builder = CreateBuilder(services,
                defaultMiddleware: mw => mw.UseMiddleware(new RecordingMiddleware(() => defaultCalled = true)));

            var pipeline = builder.AddWorker<FakeTerminalWorker>(
                middleware: mw => mw.UseMiddleware(new RecordingMiddleware(() => explicitCalled = true)));
            await using var sp = services.BuildServiceProvider();
            await pipeline.Build(sp)(new EmptyTriggerContext(DateTimeOffset.UtcNow), CancellationToken.None);

            defaultCalled.ShouldBeTrue();
            explicitCalled.ShouldBeTrue();
        }

        private static WorkerPipelineBuilder<FakeTrigger, EmptyTriggerContext> CreateBuilder(
            IServiceCollection services,
            Action<MiddlewarePipelineBuilder>? defaultMiddleware = null) =>
            new(services, WorkerPipeline.Create<EmptyTriggerContext>(), defaultMiddleware);

        private sealed class FakeTrigger : ITrigger<EmptyTriggerContext>
        {
            public Task ExecuteAsync(WorkerDelegate<EmptyTriggerContext> workerPipeline, CancellationToken stoppingToken) =>
                Task.CompletedTask;
        }

        private sealed class FakeTerminalWorker : IWorker<EmptyTriggerContext>
        {
            public Task ExecuteAsync(EmptyTriggerContext context, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class FakeTransformWorker : IWorker<EmptyTriggerContext, string>
        {
            public Task ExecuteAsync(EmptyTriggerContext source, WorkerDelegate<string> next, CancellationToken cancellationToken) =>
                next("result", cancellationToken);
        }

        private sealed class RecordingMiddleware(Action onExecute) : IWorkerMiddleware
        {
            public async Task ExecuteAsync(Func<CancellationToken, Task> next, CancellationToken cancellationToken)
            {
                onExecute();
                await next(cancellationToken);
            }
        }
    }
}
