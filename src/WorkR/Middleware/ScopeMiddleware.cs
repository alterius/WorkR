using Microsoft.Extensions.DependencyInjection;

namespace WorkR.Middleware
{
    internal sealed class ScopeMiddleware : IInternalWorkerMiddleware
    {
        public async Task ExecuteAsync(IServiceProvider sp, WorkerMiddleware next, CancellationToken cancellationToken)
        {
            await using var scope = sp.CreateAsyncScope();
            await next(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
        }
    }
}
