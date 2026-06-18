using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkR.Middleware;

namespace WorkR
{
    /// <summary>
    /// Composes a worker pipeline for a trigger, returning the completed pipeline builder.
    /// </summary>
    /// <typeparam name="TTrigger">The trigger type.</typeparam>
    /// <typeparam name="TContext">The context type the trigger produces.</typeparam>
    /// <param name="builder">The registration builder used to add workers.</param>
    /// <returns>The pipeline builder produced once the final worker has been added.</returns>
    public delegate WorkerPipelineBuilder<TContext> WorkerRegistration<TTrigger, TContext>(WorkerRegistrationBuilder<TTrigger, TContext> builder)
        where TTrigger : ITrigger<TContext>
        where TContext : TriggerContext;

    /// <summary>
    /// The entry point for composing a worker pipeline, whose first worker receives the trigger's
    /// <typeparamref name="TContext"/>.
    /// </summary>
    /// <typeparam name="TTrigger">The trigger type.</typeparam>
    /// <typeparam name="TContext">The context type the trigger produces.</typeparam>
    public sealed class WorkerRegistrationBuilder<TTrigger, TContext>
        where TTrigger : ITrigger<TContext>
        where TContext : TriggerContext
    {
        private readonly WorkerRegistrationBuilder<TTrigger, TContext, TContext> _builder;

        internal WorkerRegistrationBuilder(
            IServiceCollection services,
            WorkerPipelineBuilder<TContext, TContext> builder)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(builder);

            _builder = new WorkerRegistrationBuilder<TTrigger, TContext, TContext>(services, builder);
        }

        /// <summary>
        /// Adds a transforming worker as the first step, registered with the DI container and
        /// resolved per execution.
        /// </summary>
        /// <typeparam name="TWorker">The worker type to add.</typeparam>
        /// <typeparam name="TOut">The value type the worker forwards to the next step.</typeparam>
        /// <param name="lifetime">The lifetime to register the worker with, or <see langword="null"/> to skip registration.</param>
        /// <param name="middleware">An optional callback to configure middleware for this step.</param>
        /// <returns>A builder for adding the next worker in the chain.</returns>
        public WorkerRegistrationBuilder<TTrigger, TContext, TOut> AddWorker<TWorker, TOut>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TContext, TOut> =>
                    _builder.AddWorker<TWorker, TOut>(lifetime, middleware);

        /// <summary>
        /// Adds a transforming worker as the first step, constructed by the supplied factory once
        /// per execution and not registered with the DI container.
        /// </summary>
        /// <typeparam name="TWorker">The worker type to add.</typeparam>
        /// <typeparam name="TOut">The value type the worker forwards to the next step.</typeparam>
        /// <param name="factory">A factory that constructs the worker from the execution's service provider.</param>
        /// <param name="middleware">An optional callback to configure middleware for this step.</param>
        /// <returns>A builder for adding the next worker in the chain.</returns>
        public WorkerRegistrationBuilder<TTrigger, TContext, TOut> AddWorker<TWorker, TOut>(
            Func<IServiceProvider, TWorker> factory,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TContext, TOut> =>
                    _builder.AddWorker<TWorker, TOut>(factory, middleware);

        /// <summary>
        /// Adds a terminal worker as the final step, registered with the DI container and
        /// resolved per execution.
        /// </summary>
        /// <typeparam name="TWorker">The worker type to add.</typeparam>
        /// <param name="lifetime">The lifetime to register the worker with, or <see langword="null"/> to skip registration.</param>
        /// <param name="middleware">An optional callback to configure middleware for this step.</param>
        /// <returns>The completed pipeline builder.</returns>
        public WorkerPipelineBuilder<TContext> AddWorker<TWorker>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TContext> =>
                    _builder.AddWorker<TWorker>(lifetime, middleware);

        /// <summary>
        /// Adds a terminal worker as the final step, constructed by the supplied factory once per
        /// execution and not registered with the DI container.
        /// </summary>
        /// <typeparam name="TWorker">The worker type to add.</typeparam>
        /// <param name="factory">A factory that constructs the worker from the execution's service provider.</param>
        /// <param name="middleware">An optional callback to configure middleware for this step.</param>
        /// <returns>The completed pipeline builder.</returns>
        public WorkerPipelineBuilder<TContext> AddWorker<TWorker>(
            Func<IServiceProvider, TWorker> factory,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TContext> =>
                    _builder.AddWorker(factory, middleware);
    }

    /// <summary>
    /// A registration builder whose next worker receives the previous worker's output
    /// (<typeparamref name="TOut"/>).
    /// </summary>
    /// <typeparam name="TTrigger">The trigger type.</typeparam>
    /// <typeparam name="TContext">The context type the trigger produces.</typeparam>
    /// <typeparam name="TOut">The value type produced by the previously added worker.</typeparam>
    public sealed class WorkerRegistrationBuilder<TTrigger, TContext, TOut>
        where TTrigger : ITrigger<TContext>
        where TContext : TriggerContext
    {
        private readonly IServiceCollection _services;
        private readonly WorkerPipelineBuilder<TContext, TOut> _pipeline;

        internal WorkerRegistrationBuilder(
            IServiceCollection services,
            WorkerPipelineBuilder<TContext, TOut> pipeline)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(pipeline);

            _services = services;
            _pipeline = pipeline;
        }

        /// <summary>
        /// Adds a transforming worker that receives the previous worker's output, registered with
        /// the DI container and resolved per execution.
        /// </summary>
        /// <typeparam name="TWorker">The worker type to add.</typeparam>
        /// <typeparam name="TNext">The value type the worker forwards to the next step.</typeparam>
        /// <param name="lifetime">The lifetime to register the worker with, or <see langword="null"/> to skip registration.</param>
        /// <param name="middleware">An optional callback to configure middleware for this step.</param>
        /// <returns>A builder for adding the next worker in the chain.</returns>
        public WorkerRegistrationBuilder<TTrigger, TContext, TNext> AddWorker<TWorker, TNext>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TOut, TNext>
        {
            TryRegister<TWorker>(lifetime);
            return Then<TWorker, TNext>(sp => sp.GetRequiredService<TWorker>(), middleware);
        }

        /// <summary>
        /// Adds a transforming worker that receives the previous worker's output, constructed by
        /// the supplied factory once per execution and not registered with the DI container.
        /// </summary>
        /// <typeparam name="TWorker">The worker type to add.</typeparam>
        /// <typeparam name="TNext">The value type the worker forwards to the next step.</typeparam>
        /// <param name="factory">A factory that constructs the worker from the execution's service provider.</param>
        /// <param name="middleware">An optional callback to configure middleware for this step.</param>
        /// <returns>A builder for adding the next worker in the chain.</returns>
        public WorkerRegistrationBuilder<TTrigger, TContext, TNext> AddWorker<TWorker, TNext>(
            Func<IServiceProvider, TWorker> factory,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TOut, TNext> =>
                    Then<TWorker, TNext>(factory, middleware);

        /// <summary>
        /// Adds a terminal worker that receives the previous worker's output, registered with the
        /// DI container and resolved per execution.
        /// </summary>
        /// <typeparam name="TWorker">The worker type to add.</typeparam>
        /// <param name="lifetime">The lifetime to register the worker with, or <see langword="null"/> to skip registration.</param>
        /// <param name="middleware">An optional callback to configure middleware for this step.</param>
        /// <returns>The completed pipeline builder.</returns>
        public WorkerPipelineBuilder<TContext> AddWorker<TWorker>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TOut>
        {
            TryRegister<TWorker>(lifetime);
            return Finally(sp => sp.GetRequiredService<TWorker>(), middleware);
        }

        /// <summary>
        /// Adds a terminal worker that receives the previous worker's output, constructed by the
        /// supplied factory once per execution and not registered with the DI container.
        /// </summary>
        /// <typeparam name="TWorker">The worker type to add.</typeparam>
        /// <param name="factory">A factory that constructs the worker from the execution's service provider.</param>
        /// <param name="middleware">An optional callback to configure middleware for this step.</param>
        /// <returns>The completed pipeline builder.</returns>
        public WorkerPipelineBuilder<TContext> AddWorker<TWorker>(
            Func<IServiceProvider, TWorker> factory,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TOut> =>
                    Finally(factory, middleware);

        /// <summary>
        /// Adapts a transforming worker into a pipeline stage, binding the worker's
        /// <c>next</c> continuation to the rest of the pipeline.
        /// </summary>
        private WorkerRegistrationBuilder<TTrigger, TContext, TNext> Then<TWorker, TNext>(
            Func<IServiceProvider, TWorker> workerFactory,
            Action<WorkerMiddlewarePipelineBuilder>? middleware)
                where TWorker : IWorker<TOut, TNext>
        {
            var builder = _pipeline.Then<TNext>(
                WorkerName<TWorker>(),
                (sp, value, next, ct) => workerFactory(sp).ExecuteAsync(value, (v, ct2) => next(sp, v, ct2), ct),
                middleware);

            return new WorkerRegistrationBuilder<TTrigger, TContext, TNext>(_services, builder);
        }

        /// <summary>
        /// Adapts a terminal worker into the pipeline's final stage.
        /// </summary>
        private WorkerPipelineBuilder<TContext> Finally<TWorker>(
            Func<IServiceProvider, TWorker> workerFactory,
            Action<WorkerMiddlewarePipelineBuilder>? middleware)
                where TWorker : IWorker<TOut> =>
                    _pipeline.Finally(
                        WorkerName<TWorker>(),
                        (sp, value, ct) => workerFactory(sp).ExecuteAsync(value, ct),
                        middleware);

        private void TryRegister<TWorker>(ServiceLifetime? lifetime)
        {
            if (lifetime.HasValue)
            {
                var descriptor = ServiceDescriptor.Describe(typeof(TWorker), typeof(TWorker), lifetime.Value);
                _services.TryAdd(descriptor);
            }
        }

        private static string WorkerName<TWorker>() =>
            TypeNameHelper.GetTypeDisplayName(typeof(TWorker), fullName: false);
    }
}
