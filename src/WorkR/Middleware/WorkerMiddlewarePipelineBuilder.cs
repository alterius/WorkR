using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WorkR.Middleware
{
    internal delegate Task WorkerMiddleware(IServiceProvider serviceProvider, CancellationToken cancellationToken);
    internal delegate Task WorkerMiddlewarePipeline(IServiceProvider serviceProvider, WorkerMiddleware execute, CancellationToken cancellationToken);

    /// <summary>
    /// Builds the middleware pipeline that wraps a worker step. Middleware is applied in
    /// registration order, with the first-registered middleware outermost.
    /// </summary>
    public sealed class WorkerMiddlewarePipelineBuilder
    {
        private readonly List<Func<WorkerMiddleware, WorkerMiddleware>> _middleware = [];

        /// <summary>
        /// Adds middleware constructed per execution by the supplied factory, giving the
        /// middleware access to the execution's service provider.
        /// </summary>
        /// <typeparam name="TMiddleware">The middleware type to add.</typeparam>
        /// <param name="factory">A factory that constructs the middleware from the execution's service provider.</param>
        /// <returns>The same builder, to allow chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
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

        /// <summary>
        /// Adds a pre-constructed middleware instance. The same instance is reused for every
        /// execution, so it must be thread-safe.
        /// </summary>
        /// <param name="middleware">The middleware instance to add.</param>
        /// <returns>The same builder, to allow chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="middleware"/> is <see langword="null"/>.</exception>
        public WorkerMiddlewarePipelineBuilder UseMiddleware(IWorkerMiddleware middleware)
        {
            ArgumentNullException.ThrowIfNull(middleware);

            _middleware.Add(next => (sp, ct) => middleware.ExecuteAsync(ct2 => next(sp, ct2), ct));

            return this;
        }

        /// <summary>
        /// Adds <see cref="ErrorHandlingMiddleware{TException}"/> that catches and logs
        /// exceptions of type <typeparamref name="TException"/> thrown by downstream workers.
        /// </summary>
        /// <typeparam name="TException">The exception type to catch.</typeparam>
        /// <param name="predicate">
        /// An optional filter for the caught exception. When it returns <see langword="false"/>
        /// the exception is rethrown; when <see langword="null"/>, all matching exceptions are
        /// handled.
        /// </param>
        /// <returns>The same builder, to allow chaining.</returns>
        public WorkerMiddlewarePipelineBuilder UseErrorHandling<TException>(Func<TException, bool>? predicate = null)
            where TException : Exception =>
                UseMiddleware(sp => new ErrorHandlingMiddleware<TException>(
                    sp.GetRequiredService<ILogger<ErrorHandlingMiddleware<TException>>>(),
                    predicate));

        /// <summary>
        /// Adds <see cref="TimeoutMiddleware"/> that cancels downstream execution if it exceeds
        /// the specified duration. The timeout uses the <see cref="TimeProvider"/> registered in
        /// the container.
        /// </summary>
        /// <param name="timeout">The maximum duration before execution is cancelled. Must be positive.</param>
        /// <returns>The same builder, to allow chaining.</returns>
        public WorkerMiddlewarePipelineBuilder UseTimeout(TimeSpan timeout) =>
            UseMiddleware(sp => new TimeoutMiddleware(sp.GetRequiredService<TimeProvider>(), timeout));

        /// <summary>
        /// Adds middleware that creates an additional dependency injection scope around
        /// downstream workers. Each pipeline execution already runs in its own scope, so this is
        /// only needed to nest a further scope.
        /// </summary>
        /// <returns>The same builder, to allow chaining.</returns>
        public WorkerMiddlewarePipelineBuilder UseScope() =>
            UseInternalMiddleware(new ScopeMiddleware());

        internal WorkerMiddlewarePipelineBuilder UseInternalMiddleware<TMiddleware>(Func<IServiceProvider, TMiddleware> factory)
            where TMiddleware : IInternalWorkerMiddleware
        {
            ArgumentNullException.ThrowIfNull(factory);

            _middleware.Add(next => (sp, ct) =>
            {
                var middleware = factory(sp);
                return middleware.ExecuteAsync(sp, next, ct);
            });

            return this;
        }

        internal WorkerMiddlewarePipelineBuilder UseInternalMiddleware(IInternalWorkerMiddleware middleware)
        {
            ArgumentNullException.ThrowIfNull(middleware);

            _middleware.Add(next => (sp, ct) => middleware.ExecuteAsync(sp, next, ct));

            return this;
        }

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
