using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WorkR.Middleware
{
    internal delegate Task WorkerMiddleware(IServiceProvider serviceProvider, CancellationToken cancellationToken);
    internal delegate Task WorkerMiddlewarePipeline(IServiceProvider serviceProvider, WorkerMiddleware execute, CancellationToken cancellationToken);

    public sealed class WorkerMiddlewarePipelineBuilder
    {
        private readonly List<Func<WorkerMiddleware, WorkerMiddleware>> _middleware = [];

        public WorkerMiddlewarePipelineBuilder UseMiddleware<TMiddleware>(Func<IServiceProvider, TMiddleware> factory)
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

        public WorkerMiddlewarePipelineBuilder UseMiddleware(IWorkerMiddleware middleware)
        {
            ArgumentNullException.ThrowIfNull(middleware);

            _middleware.Add(next => (sp, ct) => middleware.ExecuteAsync(ct2 => next(sp, ct2), ct));

            return this;
        }

        public WorkerMiddlewarePipelineBuilder UseErrorHandling<TException>(Func<TException, bool>? predicate = null)
            where TException : Exception =>
                UseMiddleware(sp => new ErrorHandlingMiddleware<TException>(
                    sp.GetRequiredService<ILogger<ErrorHandlingMiddleware<TException>>>(),
                    predicate));

        public WorkerMiddlewarePipelineBuilder UseTimeout(TimeSpan timeout) =>
            UseMiddleware(sp => new TimeoutMiddleware(sp.GetRequiredService<TimeProvider>(), timeout));

        internal WorkerMiddlewarePipeline Build()
        {
            var middleware = _middleware.ToArray();

            return (sp, execute, ct) =>
            {
                var pipeline = execute;

                foreach (var stage in Enumerable.Reverse(middleware))
                {
                    pipeline = stage(pipeline);
                }

                return pipeline(sp, ct);
            };
        }
    }
}
