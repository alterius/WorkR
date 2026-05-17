using Microsoft.Extensions.DependencyInjection;

namespace WorkR.Middleware
{
    internal sealed class ScopeMiddleware : IPipelineMiddleware
    {
        public async Task Execute(IServiceProvider sp, PipelineMiddlewareDelegate next, CancellationToken ct)
        {
            await using var scope = sp.CreateAsyncScope();
            await next(scope.ServiceProvider, ct).ConfigureAwait(false);
        }
    }
}
