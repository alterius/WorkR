using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WorkR.Middleware
{
    public class MiddlewarePipelineBuilder
    {
        private readonly List<Func<ServiceProviderMiddlewareDelegate, ServiceProviderMiddlewareDelegate>> _middleware = [];

        public MiddlewarePipelineBuilder UseMiddleware<TMiddleware>(Func<IServiceProvider, TMiddleware> factory)
            where TMiddleware : IMiddleware
        {
            ArgumentNullException.ThrowIfNull(factory);

            _middleware.Add(next => (sp, ct) =>
            {
                var middleware = factory(sp);
                return middleware.Execute(ct2 => next(sp, ct2), ct);
            });

            return this;
        }

        public MiddlewarePipelineBuilder UseMiddleware(IMiddleware middleware)
        {
            ArgumentNullException.ThrowIfNull(middleware);

            _middleware.Add(next => (sp, ct) => middleware.Execute(ct2 => next(sp, ct2), ct));

            return this;
        }

        public MiddlewarePipelineBuilder UseFireAndForget() =>
            UseMiddleware(sp => new FireAndForgetMiddleware(sp.GetRequiredService<ILogger<FireAndForgetMiddleware>>()));

        public MiddlewarePipelineBuilder UseErrorHandling<TException>(Func<TException, bool>? predicate = null)
            where TException : Exception =>
                UseMiddleware(sp => new ErrorHandlingMiddleware<TException>(
                    sp.GetRequiredService<ILogger<ErrorHandlingMiddleware<TException>>>(),
                    predicate));

        public MiddlewarePipelineBuilder UseTimeout(TimeSpan timeout) =>
            UseMiddleware(sp => new TimeoutMiddleware(sp.GetRequiredService<TimeProvider>(), timeout));

        public MiddlewarePipelineBuilder UseScope() =>
            UseServiceProviderMiddleware(new ScopeMiddleware());

        internal MiddlewarePipelineBuilder UseServiceProviderMiddleware<TMiddleware>(Func<IServiceProvider, TMiddleware> factory)
            where TMiddleware : IServiceProviderMiddleware
        {
            ArgumentNullException.ThrowIfNull(factory);

            _middleware.Add(next => (sp, ct) =>
            {
                var middleware = factory(sp);
                return middleware.Execute(sp, next, ct);
            });

            return this;
        }

        internal MiddlewarePipelineBuilder UseServiceProviderMiddleware(IServiceProviderMiddleware middleware)
        {
            ArgumentNullException.ThrowIfNull(middleware);

            _middleware.Add(next => (sp, ct) => middleware.Execute(sp, next, ct));

            return this;
        }

        internal ServiceProviderMiddlewareDelegate Build(ServiceProviderMiddlewareDelegate workerExecution)
        {
            ArgumentNullException.ThrowIfNull(workerExecution);

            var pipeline = workerExecution;

            foreach (var middleware in Enumerable.Reverse(_middleware))
            {
                pipeline = middleware(pipeline);
            }

            return pipeline;
        }
    }
}
