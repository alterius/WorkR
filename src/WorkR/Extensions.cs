using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkR.Middleware;

namespace WorkR
{
    public static class Extensions
    {
        public static IServiceCollection AddWorker<TTrigger, TTriggerOut>(
            this IServiceCollection services,
            Func<IServiceProvider, TTrigger> triggerFactory,
            WorkerPipelineBuilderDelegate<TTrigger, TTriggerOut> builder,
            Action<MiddlewarePipelineBuilder>? defaultMiddleware = null)
                where TTrigger : ITrigger<TTriggerOut>
        {
            ArgumentNullException.ThrowIfNull(triggerFactory);

            var pipeline = builder(
                new WorkerPipelineBuilder<TTrigger, TTriggerOut>(
                    services,
                    WorkerPipeline.Create<TTriggerOut>(),
                    defaultMiddleware));

            services.AddSingleton<IHostedService>(sp =>
                ActivatorUtilities.CreateInstance<WorkerService<TTrigger, TTriggerOut>>(sp, triggerFactory(sp), pipeline));

            return services;
        }
    }
}
