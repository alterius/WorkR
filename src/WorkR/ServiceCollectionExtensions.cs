using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkR.Middleware;

namespace WorkR
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddWorker<TTrigger, TContext>(
            this IServiceCollection services,
            Func<IServiceProvider, TTrigger> triggerFactory,
            WorkerRegistration<TTrigger, TContext> builder,
            Action<MiddlewarePipelineBuilder>? defaultMiddleware = null)
                where TTrigger : ITrigger<TContext>
                where TContext : TriggerContext
        {
            ArgumentNullException.ThrowIfNull(triggerFactory);
            ArgumentNullException.ThrowIfNull(builder);

            var pipeline = builder(
                new WorkerRegistrationBuilder<TTrigger, TContext>(
                    services,
                    WorkerPipelineBuilder.Create<TContext>(),
                    defaultMiddleware));

            return services.AddSingleton<IHostedService>(sp =>
                new WorkerService<TTrigger, TContext>(
                    sp,
                    triggerFactory(sp),
                    pipeline,
                    sp.GetRequiredService<ILoggerFactory>()));
        }
    }
}
