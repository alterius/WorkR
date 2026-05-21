using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCrontab;
using Shouldly;

namespace WorkR.Triggers.Timers.Tests
{
    [Trait("Category", "L0")]
    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddDelayWorker_WhenBuilderIsNull_ThrowsArgumentNullException()
        {
            var services = new ServiceCollection();

            Should.Throw<ArgumentNullException>(() =>
                services.AddDelayWorker(TimeSpan.FromSeconds(1), null!));
        }

        [Fact]
        public void AddDelayWorker_WithBuilder_RegistersHostedService()
        {
            var services = new ServiceCollection();

            services.AddDelayWorker(TimeSpan.FromSeconds(1), builder => builder.AddWorker<FakeWorker>());

            services.ShouldContain(d => d.ServiceType == typeof(IHostedService));
        }

        [Fact]
        public void AddDelayWorker_Generic_RegistersHostedService()
        {
            var services = new ServiceCollection();

            services.AddDelayWorker<FakeWorker>(TimeSpan.FromSeconds(1));

            services.ShouldContain(d => d.ServiceType == typeof(IHostedService));
        }

        [Fact]
        public void AddScheduledWorker_WhenBuilderIsNull_ThrowsArgumentNullException()
        {
            var services = new ServiceCollection();

            Should.Throw<ArgumentNullException>(() =>
                services.AddScheduledWorker("* * * * *", null!));
        }

        [Fact]
        public void AddScheduledWorker_WithBuilder_RegistersHostedService()
        {
            var services = new ServiceCollection();

            services.AddScheduledWorker("* * * * *", builder => builder.AddWorker<FakeWorker>());

            services.ShouldContain(d => d.ServiceType == typeof(IHostedService));
        }

        [Fact]
        public void AddScheduledWorker_Generic_RegistersHostedService()
        {
            var services = new ServiceCollection();

            services.AddScheduledWorker<FakeWorker>("* * * * *");

            services.ShouldContain(d => d.ServiceType == typeof(IHostedService));
        }

        [Fact]
        public void AddScheduledWorker_WithCustomParseOptions_RegistersHostedService()
        {
            var services = new ServiceCollection();

            services.AddScheduledWorker("0 * * * * *",
                builder => builder.AddWorker<FakeWorker>(),
                includeSeconds: true);

            services.ShouldContain(d => d.ServiceType == typeof(IHostedService));
        }

        private sealed class FakeWorker : IWorker<EmptyTriggerContext>
        {
            public Task ExecuteAsync(EmptyTriggerContext context, CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}
