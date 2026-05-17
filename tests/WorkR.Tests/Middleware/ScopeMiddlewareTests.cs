using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WorkR.Middleware;

namespace WorkR.Tests.Middleware
{
    public class ScopeMiddlewareTests
    {
        [Fact]
        public async Task Execute_PassesScopedServiceProviderToNext()
        {
            await using var rootSp = new ServiceCollection().BuildServiceProvider();
            IServiceProvider? capturedSp = null;
            var middleware = new ScopeMiddleware();

            await middleware.Execute(rootSp, (sp, ct) =>
            {
                capturedSp = sp;
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);

            capturedSp.ShouldNotBeNull();
            capturedSp.ShouldNotBeSameAs(rootSp);
        }

        [Fact]
        public async Task Execute_DisposesScopeAfterNextCompletes()
        {
            var services = new ServiceCollection();
            services.AddScoped<DisposableService>();
            await using var rootSp = services.BuildServiceProvider();
            DisposableService? resolved = null;
            var middleware = new ScopeMiddleware();

            await middleware.Execute(rootSp, (sp, ct) =>
            {
                resolved = sp.GetRequiredService<DisposableService>();
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);

            resolved.ShouldNotBeNull();
            resolved.Disposed.ShouldBeTrue();
        }

        [Fact]
        public async Task Execute_DisposesScopeWhenNextThrows()
        {
            var services = new ServiceCollection();
            services.AddScoped<DisposableService>();
            await using var rootSp = services.BuildServiceProvider();
            DisposableService? resolved = null;
            var middleware = new ScopeMiddleware();

            await Should.ThrowAsync<InvalidOperationException>(() =>
                middleware.Execute(rootSp, (sp, ct) =>
                {
                    resolved = sp.GetRequiredService<DisposableService>();
                    return Task.FromException(new InvalidOperationException());
                }, TestContext.Current.CancellationToken));

            resolved.ShouldNotBeNull();
            resolved.Disposed.ShouldBeTrue();
        }

        private sealed class DisposableService : IAsyncDisposable
        {
            public bool Disposed { get; private set; }

            public ValueTask DisposeAsync()
            {
                Disposed = true;
                return ValueTask.CompletedTask;
            }
        }
    }
}
