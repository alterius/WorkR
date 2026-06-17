using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using WorkR.Triggers.RunOnce;

namespace WorkR.Tests.Triggers.RunOnce
{
    [Trait("Category", "L0")]
    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddRunOnceWorker_WhenBuilderIsNull_ThrowsArgumentNullException()
        {
            var services = new ServiceCollection();

            Should.Throw<ArgumentNullException>(() =>
                services.AddRunOnceWorker(null!));
        }

        [Fact]
        public void AddRunOnceWorker_WithBuilder_RegistersHostedService()
        {
            var services = new ServiceCollection();

            services.AddRunOnceWorker(builder => builder.AddWorker<FakeWorker>());

            services.ShouldContain(d => d.ServiceType == typeof(IHostedService));
        }

        [Fact]
        public void AddRunOnceWorker_Generic_RegistersHostedService()
        {
            var services = new ServiceCollection();

            services.AddRunOnceWorker<FakeWorker>();

            services.ShouldContain(d => d.ServiceType == typeof(IHostedService));
        }
    }
}
