using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkR.Middleware;

namespace WorkR
{
    public static partial class Extensions
    {
        public static IServiceCollection AddWorker<TTrigger, TOut>(
            this IServiceCollection services,
            Func<IServiceProvider, TTrigger> triggerFactory,
            Func<WorkerRegistrationBuilder<TTrigger, TOut>, TerminalWorkerRegistrationBuilder<TTrigger, TOut>> builder,
            Action<MiddlewarePipelineBuilder>? defaultMiddleware = null)
                where TTrigger : ITrigger<TOut>
        {
            ArgumentNullException.ThrowIfNull(triggerFactory);

            var worker = builder(
                new WorkerRegistrationBuilder<TTrigger, TOut>(
                    services,
                    WorkerBuilder.FromTrigger<TTrigger, TOut>(triggerFactory),
                    defaultMiddleware));

            services.AddSingleton<IHostedService>(sp =>
                ActivatorUtilities.CreateInstance<WorkerService>(sp, worker.Build()));

            return services;
        }
    }
}
