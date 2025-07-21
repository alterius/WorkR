using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkR.Middleware;

namespace WorkR
{
    public delegate TerminalWorkerRegistrationBuilder<TTrigger, TOut> WorkerRegistrationBuilderDelegate<TTrigger, TOut>(WorkerRegistrationBuilder<TTrigger, TOut> builder)
        where TTrigger : ITrigger<TOut>;

    public class WorkerRegistrationBuilder<TTrigger, TTriggerOut>
        where TTrigger : ITrigger<TTriggerOut>
    {
        private readonly WorkerRegistrationBuilder<TTrigger, TTriggerOut, TTriggerOut> _builder;
        private readonly Action<MiddlewarePipelineBuilder>? _defaultMiddleware;

        public WorkerRegistrationBuilder(
            IServiceCollection services,
            WorkerBuilder<TTrigger, TTriggerOut, TTriggerOut> builder,
            Action<MiddlewarePipelineBuilder>? defaultMiddleware = null)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(builder);

            _builder = new WorkerRegistrationBuilder<TTrigger, TTriggerOut, TTriggerOut>(services, builder);
            _defaultMiddleware = defaultMiddleware;
        }

        public WorkerRegistrationBuilder<TTrigger, TTriggerOut, TOut> RegisterWorker<TWorker, TOut>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TTriggerOut, TOut> =>
                    _builder.RegisterWorker<TWorker, TOut>(lifetime, ResolveMiddleware(middleware));

        public WorkerRegistrationBuilder<TTrigger, TTriggerOut, TOut> RegisterWorker<TWorker, TOut>(
            Func<IServiceProvider, TWorker> factory,
            ServiceLifetime lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TTriggerOut, TOut> =>
                    _builder.RegisterWorker<TWorker, TOut>(factory, lifetime, ResolveMiddleware(middleware));

        public TerminalWorkerRegistrationBuilder<TTrigger, TTriggerOut> RegisterWorker<TWorker>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TTriggerOut> =>
                    _builder.RegisterWorker<TWorker>(lifetime, ResolveMiddleware(middleware));

        public TerminalWorkerRegistrationBuilder<TTrigger, TTriggerOut> RegisterWorker<TWorker>(
            Func<IServiceProvider, TWorker> factory,
            ServiceLifetime lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TTriggerOut> =>
                    _builder.RegisterWorker(factory, lifetime, ResolveMiddleware(middleware));

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

    public class WorkerRegistrationBuilder<TTrigger, TTriggerOut, TPipeOut>
        where TTrigger : ITrigger<TTriggerOut>
    {
        private readonly IServiceCollection _services;
        private readonly WorkerBuilder<TTrigger, TTriggerOut, TPipeOut> _builder;

        public WorkerRegistrationBuilder(
            IServiceCollection services,
            WorkerBuilder<TTrigger, TTriggerOut, TPipeOut> builder)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(builder);

            _services = services;
            _builder = builder;
        }

        public WorkerRegistrationBuilder<TTrigger, TTriggerOut, TOut> RegisterWorker<TWorker, TOut>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TPipeOut, TOut>
        {
            TryRegister<TWorker>(lifetime);
            var builder = _builder.WithWorker<TWorker, TOut>(middleware);
            return new WorkerRegistrationBuilder<TTrigger, TTriggerOut, TOut>(_services, builder);
        }

        public WorkerRegistrationBuilder<TTrigger, TTriggerOut, TOut> RegisterWorker<TWorker, TOut>(
            Func<IServiceProvider, TWorker> factory,
            ServiceLifetime lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TPipeOut, TOut>
        {
            RegisterFactory(factory, lifetime);
            var builder = _builder.WithWorker<TWorker, TOut>(middleware);
            return new WorkerRegistrationBuilder<TTrigger, TTriggerOut, TOut>(_services, builder);
        }

        public TerminalWorkerRegistrationBuilder<TTrigger, TTriggerOut> RegisterWorker<TWorker>(
            ServiceLifetime? lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TPipeOut>
        {
            TryRegister<TWorker>(lifetime);
            var builder = _builder.WithWorker<TWorker>(middleware);
            return new TerminalWorkerRegistrationBuilder<TTrigger, TTriggerOut>(builder);
        }

        public TerminalWorkerRegistrationBuilder<TTrigger, TTriggerOut> RegisterWorker<TWorker>(
            Func<IServiceProvider, TWorker> factory,
            ServiceLifetime lifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TPipeOut>
        {
            RegisterFactory(factory, lifetime);
            var builder = _builder.WithWorker<TWorker>(middleware);
            return new TerminalWorkerRegistrationBuilder<TTrigger, TTriggerOut>(builder);
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

    public class TerminalWorkerRegistrationBuilder<TTrigger, TTriggerOut>
        where TTrigger : ITrigger<TTriggerOut>
    {
        private readonly TerminalWorkerBuilder<TTrigger, TTriggerOut> _builder;

        public TerminalWorkerRegistrationBuilder(TerminalWorkerBuilder<TTrigger, TTriggerOut> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            _builder = builder;
        }

        public IWorkerBuilder Build() => _builder;
    }
}
