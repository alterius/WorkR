using Microsoft.Extensions.DependencyInjection;

namespace WorkR.Middleware
{
    internal sealed class ScopeMiddleware : IServiceProviderMiddleware
    {
        public async Task Execute(IServiceProvider sp, ServiceProviderMiddlewareDelegate next, CancellationToken ct)
        {
            await using var scope = sp.CreateAsyncScope();
            await next(scope.ServiceProvider, ct).ConfigureAwait(false);
        }
    }
}
