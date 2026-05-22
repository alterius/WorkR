using Microsoft.Extensions.DependencyInjection;

namespace WorkR.Middleware
{
    internal sealed class ScopeMiddleware : IPipelineMiddleware
    {
        public async Task ExecuteAsync(IServiceProvider sp, PipelineMiddlewareDelegate next, CancellationToken cancellationToken)
        {
            await using var scope = sp.CreateAsyncScope();
            await next(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
        }
    }
}
