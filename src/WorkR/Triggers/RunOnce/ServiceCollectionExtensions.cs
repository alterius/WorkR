using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
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
                sp => new RunOnceTrigger(
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<ILogger<RunOnceTrigger>>()),
                builder,
                static mw => mw
                    .UseScope());
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
