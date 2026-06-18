using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WorkR.Middleware;

namespace WorkR.Tests
{
    [Trait("Category", "L0")]
    public class WorkerRegistrationBuilderTests
    {
        [Fact]
        public void Constructor_WhenServicesIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new WorkerRegistrationBuilder<FakeTrigger, EmptyTriggerContext>(
                    null!, WorkerPipelineBuilder.Create<EmptyTriggerContext>()));
        }

        [Fact]
        public void Constructor_WhenBuilderIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new WorkerRegistrationBuilder<FakeTrigger, EmptyTriggerContext>(
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
        public void AddWorker_Terminal_WithFactory_DoesNotRegisterWorkerInServices()
        {
            var services = new ServiceCollection();

            CreateBuilder(services).AddWorker(_ => new FakeTerminalWorker());

            services.ShouldNotContain(d => d.ServiceType == typeof(FakeTerminalWorker));
        }

        [Fact]
        public void AddWorker_Transform_WithFactory_DoesNotRegisterWorkerInServices()
        {
            var services = new ServiceCollection();

            CreateBuilder(services)
                .AddWorker<FakeTransformWorker, string>(_ => new FakeTransformWorker())
                .AddWorker(_ => new FakeStringTerminalWorker());

            services.ShouldNotContain(d => d.ServiceType == typeof(FakeTransformWorker));
        }

        [Fact]
        public async Task AddWorker_Terminal_WithFactory_InvokesFactoryPerExecution()
        {
            var services = new ServiceCollection();
            var instances = 0;

            var pipeline = CreateBuilder(services).AddWorker(_ =>
            {
                instances++;
                return new FakeTerminalWorker();
            });

            await using var sp = services.BuildServiceProvider();
            var run = pipeline.Build(sp);
            await run.ExecuteAsync(new EmptyTriggerContext(DateTimeOffset.UtcNow), CancellationToken.None);
            await run.ExecuteAsync(new EmptyTriggerContext(DateTimeOffset.UtcNow), CancellationToken.None);

            instances.ShouldBe(2);
        }

        [Fact]
        public async Task AddWorker_Transform_WithFactory_InvokesFactoryPerExecution()
        {
            var services = new ServiceCollection();
            var instances = 0;

            var pipeline = CreateBuilder(services)
                .AddWorker<FakeTransformWorker, string>(_ =>
                {
                    instances++;
                    return new FakeTransformWorker();
                })
                .AddWorker(_ => new FakeStringTerminalWorker());

            await using var sp = services.BuildServiceProvider();
            var run = pipeline.Build(sp);
            await run.ExecuteAsync(new EmptyTriggerContext(DateTimeOffset.UtcNow), CancellationToken.None);
            await run.ExecuteAsync(new EmptyTriggerContext(DateTimeOffset.UtcNow), CancellationToken.None);

            instances.ShouldBe(2);
        }

        [Fact]
        public void AddWorker_InnerTransform_WithFactory_DoesNotRegisterWorkerInServices()
        {
            var services = new ServiceCollection();

            CreateBuilder(services)
                .AddWorker<FakeTransformWorker, string>(_ => new FakeTransformWorker())
                .AddWorker<FakeStringTransformWorker, int>(_ => new FakeStringTransformWorker())
                .AddWorker(_ => new FakeIntTerminalWorker());

            services.ShouldNotContain(d => d.ServiceType == typeof(FakeStringTransformWorker));
        }

        [Fact]
        public async Task AddWorker_InnerTransform_WithFactory_InvokesFactoryPerExecution()
        {
            var services = new ServiceCollection();
            var instances = 0;

            var pipeline = CreateBuilder(services)
                .AddWorker<FakeTransformWorker, string>(_ => new FakeTransformWorker())
                .AddWorker<FakeStringTransformWorker, int>(_ =>
                {
                    instances++;
                    return new FakeStringTransformWorker();
                })
                .AddWorker(_ => new FakeIntTerminalWorker());

            await using var sp = services.BuildServiceProvider();
            var run = pipeline.Build(sp);
            await run.ExecuteAsync(new EmptyTriggerContext(DateTimeOffset.UtcNow), CancellationToken.None);
            await run.ExecuteAsync(new EmptyTriggerContext(DateTimeOffset.UtcNow), CancellationToken.None);

            instances.ShouldBe(2);
        }

        [Fact]
        public void AddWorker_Terminal_WithLifetime_RegistersWorkerWithThatLifetime()
        {
            var services = new ServiceCollection();

            CreateBuilder(services).AddWorker<FakeTerminalWorker>(ServiceLifetime.Singleton);

            services.ShouldContain(d =>
                d.ServiceType == typeof(FakeTerminalWorker) &&
                d.ImplementationType == typeof(FakeTerminalWorker) &&
                d.Lifetime == ServiceLifetime.Singleton);
        }

        [Fact]
        public void AddWorker_Transform_WithLifetime_RegistersWorkerWithThatLifetime()
        {
            var services = new ServiceCollection();

            CreateBuilder(services).AddWorker<FakeTransformWorker, string>(ServiceLifetime.Scoped);

            services.ShouldContain(d =>
                d.ServiceType == typeof(FakeTransformWorker) &&
                d.ImplementationType == typeof(FakeTransformWorker) &&
                d.Lifetime == ServiceLifetime.Scoped);
        }

        [Fact]
        public void AddWorker_Transform_WithLifetime_DefaultsToTransient()
        {
            var services = new ServiceCollection();

            CreateBuilder(services).AddWorker<FakeTransformWorker, string>();

            services.ShouldContain(d =>
                d.ServiceType == typeof(FakeTransformWorker) &&
                d.Lifetime == ServiceLifetime.Transient);
        }

        [Fact]
        public void AddWorker_InnerTransform_WithLifetime_RegistersWorkerWithThatLifetime()
        {
            var services = new ServiceCollection();

            CreateBuilder(services)
                .AddWorker<FakeTransformWorker, string>(_ => new FakeTransformWorker())
                .AddWorker<FakeStringTransformWorker, int>(ServiceLifetime.Singleton);

            services.ShouldContain(d =>
                d.ServiceType == typeof(FakeStringTransformWorker) &&
                d.ImplementationType == typeof(FakeStringTransformWorker) &&
                d.Lifetime == ServiceLifetime.Singleton);
        }

        [Fact]
        public void AddWorker_InnerTerminal_WithLifetime_RegistersWorkerWithThatLifetime()
        {
            var services = new ServiceCollection();

            CreateBuilder(services)
                .AddWorker<FakeTransformWorker, string>(_ => new FakeTransformWorker())
                .AddWorker<FakeStringTerminalWorker>(ServiceLifetime.Scoped);

            services.ShouldContain(d =>
                d.ServiceType == typeof(FakeStringTerminalWorker) &&
                d.ImplementationType == typeof(FakeStringTerminalWorker) &&
                d.Lifetime == ServiceLifetime.Scoped);
        }

        [Fact]
        public async Task AddWorker_WithExplicitMiddleware_AppliesIt()
        {
            var explicitCalled = false;
            var services = new ServiceCollection().AddSingleton<FakeTerminalWorker>();
            var builder = CreateBuilder(services);

            var pipeline = builder.AddWorker<FakeTerminalWorker>(
                middleware: mw => mw.UseMiddleware(new RecordingMiddleware(() => explicitCalled = true)));
            await using var sp = services.BuildServiceProvider();
            await pipeline.Build(sp).ExecuteAsync(new EmptyTriggerContext(DateTimeOffset.UtcNow), CancellationToken.None);

            explicitCalled.ShouldBeTrue();
        }

        private static WorkerRegistrationBuilder<FakeTrigger, EmptyTriggerContext> CreateBuilder(
            IServiceCollection services) =>
            new(services, WorkerPipelineBuilder.Create<EmptyTriggerContext>());

        private sealed class FakeTerminalWorker : IWorker<EmptyTriggerContext>
        {
            public Task ExecuteAsync(EmptyTriggerContext context, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class FakeTransformWorker : IWorker<EmptyTriggerContext, string>
        {
            public Task ExecuteAsync(EmptyTriggerContext source, Worker<string> next, CancellationToken cancellationToken) =>
                next("result", cancellationToken);
        }

        private sealed class FakeStringTerminalWorker : IWorker<string>
        {
            public Task ExecuteAsync(string context, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class FakeStringTransformWorker : IWorker<string, int>
        {
            public Task ExecuteAsync(string source, Worker<int> next, CancellationToken cancellationToken) =>
                next(0, cancellationToken);
        }

        private sealed class FakeIntTerminalWorker : IWorker<int>
        {
            public Task ExecuteAsync(int context, CancellationToken cancellationToken) => Task.CompletedTask;
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
