using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WorkR.Middleware;

namespace WorkR.Tests.Middleware
{
    [Trait("Category", "L0")]
    public class ScopeMiddlewareTests
    {
        [Fact]
        public async Task ExecuteAsync_PassesScopedServiceProviderToNext()
        {
            await using var rootSp = new ServiceCollection().BuildServiceProvider();
            IServiceProvider? capturedSp = null;
            var middleware = new ScopeMiddleware();

            await middleware.ExecuteAsync(rootSp, (sp, ct) =>
            {
                capturedSp = sp;
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);

            capturedSp.ShouldNotBeNull();
            capturedSp.ShouldNotBeSameAs(rootSp);
        }

        [Fact]
        public async Task ExecuteAsync_DoesNotResolveRootServiceProvider()
        {
            await using var rootSp = new ServiceCollection().BuildServiceProvider();
            var middleware = new ScopeMiddleware();

            var resolvedRootSp = rootSp.GetRequiredService<IServiceProvider>();
            IServiceProvider? capturedServiceProvider = null;
            IServiceProvider? resolvedServiceProvider = null;

            await middleware.ExecuteAsync(rootSp, (sp, ct) =>
            {
                capturedServiceProvider = sp;
                resolvedServiceProvider = sp.GetRequiredService<IServiceProvider>();
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);

            capturedServiceProvider.ShouldNotBe(resolvedRootSp);
            resolvedServiceProvider.ShouldNotBe(resolvedRootSp);
            capturedServiceProvider.ShouldBe(resolvedServiceProvider);
        }

        [Fact]
        public async Task ExecuteAsync_DisposesScopeAfterNextCompletes()
        {
            var services = new ServiceCollection();
            services.AddScoped<DisposableService>();
            await using var rootSp = services.BuildServiceProvider();
            DisposableService? resolved = null;
            var middleware = new ScopeMiddleware();

            await middleware.ExecuteAsync(rootSp, (sp, ct) =>
            {
                resolved = sp.GetRequiredService<DisposableService>();
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);

            resolved.ShouldNotBeNull();
            resolved.Disposed.ShouldBeTrue();
        }

        [Fact]
        public async Task ExecuteAsync_DisposesScopeWhenNextThrows()
        {
            var services = new ServiceCollection();
            services.AddScoped<DisposableService>();
            await using var rootSp = services.BuildServiceProvider();
            DisposableService? resolved = null;
            var middleware = new ScopeMiddleware();

            await Should.ThrowAsync<InvalidOperationException>(() =>
                middleware.ExecuteAsync(rootSp, (sp, ct) =>
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
