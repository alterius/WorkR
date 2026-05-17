using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkR.Middleware;

namespace WorkR.Triggers.RunOnce
{
    public static class ServiceCollectionExtensions
    {
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
