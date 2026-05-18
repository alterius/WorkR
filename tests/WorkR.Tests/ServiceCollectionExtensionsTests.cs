using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace WorkR.Tests
{
    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddWorker_WhenTriggerFactoryIsNull_ThrowsArgumentNullException()
        {
            var services = new ServiceCollection();

            Should.Throw<ArgumentNullException>(() =>
                services.AddWorker<FakeTrigger, EmptyTriggerContext>(
                    (Func<IServiceProvider, FakeTrigger>)null!,
                    builder => builder.AddWorker<FakeWorker>()));
        }

        [Fact]
        public void AddWorker_WhenBuilderIsNull_ThrowsArgumentNullException()
        {
            var services = new ServiceCollection();

            Should.Throw<ArgumentNullException>(() =>
                services.AddWorker(
                    _ => new FakeTrigger(),
                    (WorkerPipelineBuilderDelegate<FakeTrigger, EmptyTriggerContext>)null!));
        }

        [Fact]
        public void AddWorker_WithFactory_RegistersHostedService()
        {
            var services = new ServiceCollection();

            services.AddWorker<FakeTrigger, EmptyTriggerContext>(
                _ => new FakeTrigger(),
                builder => builder.AddWorker<FakeWorker>());

            services.ShouldContain(d => d.ServiceType == typeof(IHostedService));
        }

        [Fact]
        public void AddWorker_WithInstance_RegistersHostedService()
        {
            var services = new ServiceCollection();

            services.AddWorker<FakeTrigger, EmptyTriggerContext>(
                new FakeTrigger(),
                builder => builder.AddWorker<FakeWorker>());

            services.ShouldContain(d => d.ServiceType == typeof(IHostedService));
        }

        private sealed class FakeTrigger : ITrigger<EmptyTriggerContext>
        {
            public Task Execute(WorkerDelegate<EmptyTriggerContext> next, CancellationToken ct) =>
                Task.CompletedTask;
        }

        private sealed class FakeWorker : IWorker<EmptyTriggerContext>
        {
            public Task Execute(EmptyTriggerContext context, CancellationToken ct) => Task.CompletedTask;
        }
    }
}
