using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkR.Middleware;

namespace WorkR
{
    public delegate WorkerPipelineBuilder<TContext> WorkerRegistration<TTrigger, TContext>(WorkerRegistrationBuilder<TTrigger, TContext> builder)
        where TTrigger : ITrigger<TContext>
        where TContext : TriggerContext;

    public sealed class WorkerRegistrationBuilder<TTrigger, TContext>
        where TTrigger : ITrigger<TContext>
        where TContext : TriggerContext
    {
        private readonly WorkerPipelineBuilder<TTrigger, TContext, TContext> _builder;
        private readonly Action<WorkerMiddlewarePipelineBuilder>? _defaultMiddleware;

        internal WorkerRegistrationBuilder(
            IServiceCollection services,
            WorkerPipelineBuilder<TContext, TContext> builder,
            Action<WorkerMiddlewarePipelineBuilder>? defaultMiddleware = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(builder);

            _builder = new WorkerPipelineBuilder<TTrigger, TContext, TContext>(services, builder);
            _defaultMiddleware = defaultMiddleware;
        }

        public WorkerPipelineBuilder<TTrigger, TContext, TOut> AddWorker<TWorker, TOut>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TContext, TOut> =>
                    _builder.AddWorker<TWorker, TOut>(lifetime, ResolveMiddleware(middleware));

        public WorkerPipelineBuilder<TTrigger, TContext, TOut> AddWorker<TWorker, TOut>(
            Func<IServiceProvider, TWorker> factory,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TContext, TOut> =>
                    _builder.AddWorker<TWorker, TOut>(factory, ResolveMiddleware(middleware));

        public WorkerPipelineBuilder<TContext> AddWorker<TWorker>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TContext> =>
                    _builder.AddWorker<TWorker>(lifetime, ResolveMiddleware(middleware));

        public WorkerPipelineBuilder<TContext> AddWorker<TWorker>(
            Func<IServiceProvider, TWorker> factory,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TContext> =>
                    _builder.AddWorker(factory, ResolveMiddleware(middleware));

        private Action<WorkerMiddlewarePipelineBuilder>? ResolveMiddleware(Action<WorkerMiddlewarePipelineBuilder>? middleware)
        {
            if (_defaultMiddleware == null)
            {
                return middleware;
            }

            if (middleware == null)
            {
                return _defaultMiddleware;
            }

            return mw => {
                _defaultMiddleware(mw);
                middleware(mw);
            };
        }
    }

    public sealed class WorkerPipelineBuilder<TTrigger, TContext, TOut>
        where TTrigger : ITrigger<TContext>
        where TContext : TriggerContext
    {
        private readonly IServiceCollection _services;
        private readonly WorkerPipelineBuilder<TContext, TOut> _pipeline;

        internal WorkerPipelineBuilder(
            IServiceCollection services,
            WorkerPipelineBuilder<TContext, TOut> pipeline)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(pipeline);

            _services = services;
            _pipeline = pipeline;
        }

        public WorkerPipelineBuilder<TTrigger, TContext, TNext> AddWorker<TWorker, TNext>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TOut, TNext>
        {
            TryRegister<TWorker>(lifetime);
            return Then<TWorker, TNext>(sp => sp.GetRequiredService<TWorker>(), middleware);
        }

        public WorkerPipelineBuilder<TTrigger, TContext, TNext> AddWorker<TWorker, TNext>(
            Func<IServiceProvider, TWorker> factory,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TOut, TNext> =>
                    Then<TWorker, TNext>(factory, middleware);

        public WorkerPipelineBuilder<TContext> AddWorker<TWorker>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TOut>
        {
            TryRegister<TWorker>(lifetime);
            return Finally(sp => sp.GetRequiredService<TWorker>(), middleware);
        }

        public WorkerPipelineBuilder<TContext> AddWorker<TWorker>(
            Func<IServiceProvider, TWorker> factory,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TOut> =>
                    Finally(factory, middleware);

        private WorkerPipelineBuilder<TTrigger, TContext, TNext> Then<TWorker, TNext>(
            Func<IServiceProvider, TWorker> workerFactory,
            Action<WorkerMiddlewarePipelineBuilder>? middleware)
                where TWorker : IWorker<TOut, TNext>
        {
            var builder = _pipeline.Then<TNext>(
                WorkerName<TWorker>(),
                (sp, value, next, ct) => workerFactory(sp).ExecuteAsync(value, WorkerPipeline.Create<TNext>((v, ct2) => next(sp, v, ct2)), ct),
                middleware);
            return new WorkerPipelineBuilder<TTrigger, TContext, TNext>(_services, builder);
        }

        private WorkerPipelineBuilder<TContext> Finally<TWorker>(
            Func<IServiceProvider, TWorker> workerFactory,
            Action<WorkerMiddlewarePipelineBuilder>? middleware)
                where TWorker : IWorker<TOut> =>
                    _pipeline.Finally(
                        WorkerName<TWorker>(),
                        (sp, value, ct) => workerFactory(sp).ExecuteAsync(value, ct),
                        middleware);

        private static string WorkerName<TWorker>() =>
            TypeNameHelper.GetTypeDisplayName(typeof(TWorker), fullName: false);

        private void TryRegister<TWorker>(ServiceLifetime? lifetime)
        {
            if (lifetime.HasValue)
            {
                var descriptor = ServiceDescriptor.Describe(typeof(TWorker), typeof(TWorker), lifetime.Value);
                _services.TryAdd(descriptor);
            }
        }
    }
}
