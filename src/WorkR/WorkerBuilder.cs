using Microsoft.Extensions.DependencyInjection;
using WorkR.Middleware;

namespace WorkR
{
    public delegate Task WorkerDelegate<TOut>(IServiceProvider sp, TOut value, CancellationToken ct);
    public delegate Task TriggerDelegate<TTrigger, TOut>(IServiceProvider sp, TTrigger trigger, WorkerDelegate<TOut> next, CancellationToken ct);
    public delegate Task TerminalWorkerDelegate<TTrigger>(IServiceProvider sp, TTrigger trigger, CancellationToken ct);

    public class WorkerBuilder
    {
        public static WorkerBuilder<TTrigger, TTriggerOut, TTriggerOut> FromTrigger<TTrigger, TTriggerOut>(Func<IServiceProvider, TTrigger> triggerFactory)
            where TTrigger : ITrigger<TTriggerOut> =>
                new(triggerFactory, (sp, trigger, next, ct) => trigger.Execute((value, ct2) => next(sp, value, ct2), ct));
    }

    public class WorkerBuilder<TTrigger, TTriggerOut, TPipeOut>
        where TTrigger : ITrigger<TTriggerOut>
    {
        private readonly Func<IServiceProvider, TTrigger> _triggerFactory;
        private readonly TriggerDelegate<TTrigger, TPipeOut> _pipeline;

        public WorkerBuilder(
            Func<IServiceProvider, TTrigger> triggerFactory,
            TriggerDelegate<TTrigger, TPipeOut> pipeline)
        {
            ArgumentNullException.ThrowIfNull(triggerFactory);
            ArgumentNullException.ThrowIfNull(pipeline);

            _triggerFactory = triggerFactory;
            _pipeline = pipeline;
        }

        public WorkerBuilder<TTrigger, TTriggerOut, TOut> WithWorker<TWorker, TOut>(
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TPipeOut, TOut>
        {
            var current = _pipeline;
            var applyMiddleware = BuildMiddlewareStep(middleware);

            return new WorkerBuilder<TTrigger, TTriggerOut, TOut>(
                _triggerFactory,
                (sp, trigger, next, ct) => current(sp, trigger, (sp2, value, ct2) =>
                    applyMiddleware((sp3, ct3) =>
                    {
                        var worker = sp3.GetRequiredService<TWorker>();
                        return worker.Execute(value, (v, ct4) => next(sp3, v, ct4), ct3);
                    }).Invoke(sp2, ct2), ct));
        }

        public TerminalWorkerBuilder<TTrigger, TTriggerOut> WithWorker<TWorker>(
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TPipeOut>
        {
            var current = _pipeline;
            var applyMiddleware = BuildMiddlewareStep(middleware);

            return new TerminalWorkerBuilder<TTrigger, TTriggerOut>(
                _triggerFactory,
                (sp, trigger, ct) => current(sp, trigger, (sp2, value, ct2) =>
                    applyMiddleware((sp3, ct3) =>
                    {
                        var worker = sp3.GetRequiredService<TWorker>();
                        return worker.Execute(value, ct3);
                    }).Invoke(sp2, ct2), ct));
        }

        private static Func<ServiceProviderMiddlewareDelegate, ServiceProviderMiddlewareDelegate> BuildMiddlewareStep(
            Action<MiddlewarePipelineBuilder>? configure)
        {
            var builder = new MiddlewarePipelineBuilder();
            configure?.Invoke(builder);
            return builder.Build;
        }
    }

    public class TerminalWorkerBuilder<TTrigger, TTriggerOut> : IWorkerBuilder
        where TTrigger : ITrigger<TTriggerOut>
    {
        private readonly Func<IServiceProvider, TTrigger> _triggerFactory;
        private readonly TerminalWorkerDelegate<TTrigger> _pipeline;

        public TerminalWorkerBuilder(
            Func<IServiceProvider, TTrigger> triggerFactory,
            TerminalWorkerDelegate<TTrigger> pipeline)
        {
            ArgumentNullException.ThrowIfNull(triggerFactory);
            ArgumentNullException.ThrowIfNull(pipeline);

            _triggerFactory = triggerFactory;
            _pipeline = pipeline;
        }

        public Func<CancellationToken, Task> Build(IServiceProvider serviceProvider)
        {
            return ct =>
            {
                var trigger = _triggerFactory(serviceProvider);
                return _pipeline(serviceProvider, trigger, ct);
            };
        }
    }
}
