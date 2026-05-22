using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WorkR.Middleware
{
    public sealed class MiddlewarePipelineBuilder
    {
        private readonly List<Func<PipelineMiddlewareDelegate, PipelineMiddlewareDelegate>> _middleware = [];

        public MiddlewarePipelineBuilder UseMiddleware<TMiddleware>(Func<IServiceProvider, TMiddleware> factory)
            where TMiddleware : IWorkerMiddleware
        {
            ArgumentNullException.ThrowIfNull(factory);

            _middleware.Add(next => (sp, ct) =>
            {
                var middleware = factory(sp);
                return middleware.ExecuteAsync(ct2 => next(sp, ct2), ct);
            });

            return this;
        }

        public MiddlewarePipelineBuilder UseMiddleware(IWorkerMiddleware middleware)
        {
            ArgumentNullException.ThrowIfNull(middleware);

            _middleware.Add(next => (sp, ct) => middleware.ExecuteAsync(ct2 => next(sp, ct2), ct));

            return this;
        }

        public MiddlewarePipelineBuilder UseErrorHandling<TException>(Func<TException, bool>? predicate = null)
            where TException : Exception =>
                UseMiddleware(sp => new ErrorHandlingMiddleware<TException>(
                    sp.GetRequiredService<ILogger<ErrorHandlingMiddleware<TException>>>(),
                    predicate));

        public MiddlewarePipelineBuilder UseTimeout(TimeSpan timeout) =>
            UseMiddleware(sp => new TimeoutMiddleware(sp.GetRequiredService<TimeProvider>(), timeout));

        public MiddlewarePipelineBuilder UseScope() =>
            UsePipelineMiddleware(new ScopeMiddleware());

        internal MiddlewarePipelineBuilder UsePipelineMiddleware<TMiddleware>(Func<IServiceProvider, TMiddleware> factory)
            where TMiddleware : IPipelineMiddleware
        {
            ArgumentNullException.ThrowIfNull(factory);

            _middleware.Add(next => (sp, ct) =>
            {
                var middleware = factory(sp);
                return middleware.ExecuteAsync(sp, next, ct);
            });

            return this;
        }

        internal MiddlewarePipelineBuilder UsePipelineMiddleware(IPipelineMiddleware middleware)
        {
            ArgumentNullException.ThrowIfNull(middleware);

            _middleware.Add(next => (sp, ct) => middleware.ExecuteAsync(sp, next, ct));

            return this;
        }

        internal PipelineMiddlewareDelegate Build(PipelineMiddlewareDelegate workerExecution)
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
