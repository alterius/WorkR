using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkR.Middleware;

namespace WorkR
{
    public delegate WorkerPipeline<TContext> WorkerPipelineBuilderDelegate<TTrigger, TContext>(WorkerPipelineBuilder<TTrigger, TContext> builder)
        where TTrigger : ITrigger<TContext>
        where TContext : TriggerContext;

    public sealed class WorkerPipelineBuilder<TTrigger, TContext>
        where TTrigger : ITrigger<TContext>
        where TContext : TriggerContext
    {
        private readonly WorkerPipelineBuilder<TTrigger, TContext, TContext> _builder;
        private readonly Action<MiddlewarePipelineBuilder>? _defaultMiddleware;

        internal WorkerPipelineBuilder(
            IServiceCollection services,
            WorkerPipeline<TContext, TContext> builder,
            Action<MiddlewarePipelineBuilder>? defaultMiddleware = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(builder);

            _builder = new WorkerPipelineBuilder<TTrigger, TContext, TContext>(services, builder);
            _defaultMiddleware = defaultMiddleware;
        }

        public WorkerPipelineBuilder<TTrigger, TContext, TOut> AddWorker<TWorker, TOut>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TContext, TOut> =>
                    _builder.AddWorker<TWorker, TOut>(lifetime, ResolveMiddleware(middleware));

        public WorkerPipelineBuilder<TTrigger, TContext, TOut> AddWorker<TWorker, TOut>(
            Func<IServiceProvider, TWorker> factory,
            ServiceLifetime lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TContext, TOut> =>
                    _builder.AddWorker<TWorker, TOut>(factory, lifetime, ResolveMiddleware(middleware));

        public WorkerPipeline<TContext> AddWorker<TWorker>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TContext> =>
                    _builder.AddWorker<TWorker>(lifetime, ResolveMiddleware(middleware));

        public WorkerPipeline<TContext> AddWorker<TWorker>(
            Func<IServiceProvider, TWorker> factory,
            ServiceLifetime lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TContext> =>
                    _builder.AddWorker(factory, lifetime, ResolveMiddleware(middleware));

        private Action<MiddlewarePipelineBuilder>? ResolveMiddleware(Action<MiddlewarePipelineBuilder>? middleware)
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
        private readonly WorkerPipeline<TContext, TOut> _pipeline;

        internal WorkerPipelineBuilder(
            IServiceCollection services,
            WorkerPipeline<TContext, TOut> pipeline)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(pipeline);

            _services = services;
            _pipeline = pipeline;
        }

        public WorkerPipelineBuilder<TTrigger, TContext, TNext> AddWorker<TWorker, TNext>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TOut, TNext>
        {
            TryRegister<TWorker>(lifetime);
            return Then<TWorker, TNext>(sp => sp.GetRequiredService<TWorker>(), middleware);
        }

        public WorkerPipelineBuilder<TTrigger, TContext, TNext> AddWorker<TWorker, TNext>(
            Func<IServiceProvider, TWorker> factory,
            ServiceLifetime lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TOut, TNext>
        {
            RegisterFactory(factory, lifetime);
            return Then<TWorker, TNext>(factory, middleware);
        }

        public WorkerPipeline<TContext> AddWorker<TWorker>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TOut>
        {
            TryRegister<TWorker>(lifetime);
            return Finally<TWorker>(sp => sp.GetRequiredService<TWorker>(), middleware);
        }

        public WorkerPipeline<TContext> AddWorker<TWorker>(
            Func<IServiceProvider, TWorker> factory,
            ServiceLifetime lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TOut>
        {
            RegisterFactory(factory, lifetime);
            return Finally<TWorker>(factory, middleware);
        }

        private WorkerPipelineBuilder<TTrigger, TContext, TNext> Then<TWorker, TNext>(
            Func<IServiceProvider, TWorker> workerFactory,
            Action<MiddlewarePipelineBuilder>? middleware)
                where TWorker : IWorker<TOut, TNext>
        {
            var builder = _pipeline.Then<TNext>(
                WorkerName<TWorker>(),
                (sp, value, next, ct) => workerFactory(sp).ExecuteAsync(value, (v, ct2) => next(sp, v, ct2), ct),
                middleware);
            return new WorkerPipelineBuilder<TTrigger, TContext, TNext>(_services, builder);
        }

        private WorkerPipeline<TContext> Finally<TWorker>(
            Func<IServiceProvider, TWorker> workerFactory,
            Action<MiddlewarePipelineBuilder>? middleware)
                where TWorker : IWorker<TOut> =>
                    _pipeline.Finally(
                        WorkerName<TWorker>(),
                        (sp, value, ct) => workerFactory(sp).ExecuteAsync(value, ct),
                        middleware);

        private static string WorkerName<TWorker>() =>
            TypeNameHelper.GetTypeDisplayName(typeof(TWorker), fullName: false);

        private void RegisterFactory<TWorker>(Func<IServiceProvider, TWorker> factory, ServiceLifetime lifetime)
            where TWorker : notnull
        {
            var descriptor = ServiceDescriptor.Describe(typeof(TWorker), sp => factory(sp), lifetime);
            _services.TryAdd(descriptor);
        }

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
