using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using WorkR.Middleware;

namespace WorkR
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddWorker<TTrigger, TContext>(
            this IServiceCollection services,
            Func<IServiceProvider, TTrigger> triggerFactory,
            WorkerPipelineBuilderDelegate<TTrigger, TContext> builder,
            Action<MiddlewarePipelineBuilder>? defaultMiddleware = null)
                where TTrigger : ITrigger<TContext>
                where TContext : TriggerContext
        {
            ArgumentNullException.ThrowIfNull(triggerFactory);
            ArgumentNullException.ThrowIfNull(builder);

            var pipeline = builder(
                new WorkerPipelineBuilder<TTrigger, TContext>(
                    services,
                    WorkerPipeline.Create<TContext>(),
                    defaultMiddleware));

            services.AddSingleton<IHostedService>(sp =>
                ActivatorUtilities.CreateInstance<WorkerService<TTrigger, TContext>>(sp, triggerFactory(sp), pipeline));

            return services;
        }

        public static IServiceCollection AddWorker<TTrigger, TContext>(
            this IServiceCollection services,
            TTrigger trigger,
            WorkerPipelineBuilderDelegate<TTrigger, TContext> builder,
            Action<MiddlewarePipelineBuilder>? defaultMiddleware = null)
                where TTrigger : ITrigger<TContext>
                where TContext : TriggerContext =>
                    AddWorker(services, _ => trigger, builder, defaultMiddleware);

        public static IServiceCollection AddRunOnceWorker(
            this IServiceCollection services,
            WorkerPipelineBuilderDelegate<RunOnceTrigger, EmptyTriggerContext> builder)
        {
            services.TryAddSingleton(TimeProvider.System);

            return services.AddWorker(
                sp => ActivatorUtilities.CreateInstance<RunOnceTrigger>(sp),
                builder,
                static mw => mw.UseScope()
                    .UseErrorHandling<Exception>(ex => ex is not OperationCanceledException));
        }

        public static IServiceCollection AddRunOnceWorker<TWorker>(
            this IServiceCollection services,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<EmptyTriggerContext>
        {
            return services.AddRunOnceWorker(
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware));
        }
    }
}
