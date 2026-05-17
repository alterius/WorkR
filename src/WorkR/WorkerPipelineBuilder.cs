using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkR.Middleware;

namespace WorkR
{
    public delegate WorkerPipeline<TTriggerOut> WorkerPipelineBuilderDelegate<TTrigger, TTriggerOut>(WorkerPipelineBuilder<TTrigger, TTriggerOut> builder)
        where TTrigger : ITrigger<TTriggerOut>;

    public class WorkerPipelineBuilder<TTrigger, TTriggerOut>
        where TTrigger : ITrigger<TTriggerOut>
    {
        private readonly WorkerPipelineBuilder<TTrigger, TTriggerOut, TTriggerOut> _builder;
        private readonly Action<MiddlewarePipelineBuilder>? _defaultMiddleware;

        public WorkerPipelineBuilder(
            IServiceCollection services,
            WorkerPipeline<TTriggerOut, TTriggerOut> builder,
            Action<MiddlewarePipelineBuilder>? defaultMiddleware = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(builder);

            _builder = new WorkerPipelineBuilder<TTrigger, TTriggerOut, TTriggerOut>(services, builder);
            _defaultMiddleware = defaultMiddleware;
        }

        public WorkerPipelineBuilder<TTrigger, TTriggerOut, TOut> AddWorker<TWorker, TOut>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TTriggerOut, TOut> =>
                    _builder.AddWorker<TWorker, TOut>(lifetime, ResolveMiddleware(middleware));

        public WorkerPipelineBuilder<TTrigger, TTriggerOut, TOut> AddWorker<TWorker, TOut>(
            Func<IServiceProvider, TWorker> factory,
            ServiceLifetime lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TTriggerOut, TOut> =>
                    _builder.AddWorker<TWorker, TOut>(factory, lifetime, ResolveMiddleware(middleware));

        public WorkerPipeline<TTriggerOut> AddWorker<TWorker>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TTriggerOut> =>
                    _builder.AddWorker<TWorker>(lifetime, ResolveMiddleware(middleware));

        public WorkerPipeline<TTriggerOut> AddWorker<TWorker>(
            Func<IServiceProvider, TWorker> factory,
            ServiceLifetime lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TTriggerOut> =>
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

    public class WorkerPipelineBuilder<TTrigger, TTriggerOut, TOut>
        where TTrigger : ITrigger<TTriggerOut>
    {
        private readonly IServiceCollection _services;
        private readonly WorkerPipeline<TTriggerOut, TOut> _pipeline;

        public WorkerPipelineBuilder(
            IServiceCollection services,
            WorkerPipeline<TTriggerOut, TOut> pipeline)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(pipeline);

            _services = services;
            _pipeline = pipeline;
        }

        public WorkerPipelineBuilder<TTrigger, TTriggerOut, TNext> AddWorker<TWorker, TNext>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TOut, TNext>
        {
            TryRegister<TWorker>(lifetime);
            var builder = _pipeline.Then<TWorker, TNext>(middleware);
            return new WorkerPipelineBuilder<TTrigger, TTriggerOut, TNext>(_services, builder);
        }

        public WorkerPipelineBuilder<TTrigger, TTriggerOut, TNext> AddWorker<TWorker, TNext>(
            Func<IServiceProvider, TWorker> factory,
            ServiceLifetime lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TOut, TNext>
        {
            RegisterFactory(factory, lifetime);
            var builder = _pipeline.Then<TWorker, TNext>(middleware);
            return new WorkerPipelineBuilder<TTrigger, TTriggerOut, TNext>(_services, builder);
        }

        public WorkerPipeline<TTriggerOut> AddWorker<TWorker>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TOut>
        {
            TryRegister<TWorker>(lifetime);
            return _pipeline.Finally<TWorker>(middleware);
        }

        public WorkerPipeline<TTriggerOut> AddWorker<TWorker>(
            Func<IServiceProvider, TWorker> factory,
            ServiceLifetime lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TOut>
        {
            RegisterFactory(factory, lifetime);
            return _pipeline.Finally<TWorker>(middleware);
        }

        private void RegisterFactory<TWorker>(Func<IServiceProvider, TWorker> factory, ServiceLifetime lifetime)
            where TWorker : notnull
        {
            var descriptor = ServiceDescriptor.Describe(typeof(TWorker), sp => factory(sp), lifetime);
            _services.Add(descriptor);
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
