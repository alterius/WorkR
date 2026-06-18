using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WorkR
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddWorker<TTrigger, TContext>(
            this IServiceCollection services,
            Func<IServiceProvider, TTrigger> triggerFactory,
            WorkerRegistration<TTrigger, TContext> builder)
                where TTrigger : ITrigger<TContext>
                where TContext : TriggerContext
        {
            ArgumentNullException.ThrowIfNull(triggerFactory);
            ArgumentNullException.ThrowIfNull(builder);

            var pipeline = builder(
                new WorkerRegistrationBuilder<TTrigger, TContext>(
                    services,
                    WorkerPipelineBuilder.Create<TContext>()));

            return services.AddSingleton<IHostedService>(sp =>
                new WorkerService<TTrigger, TContext>(
                    triggerFactory(sp),
                    pipeline.Build(sp),
                    sp.GetRequiredService<ILoggerFactory>()));
        }
    }
}
