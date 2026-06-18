using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WorkR.Middleware;

namespace WorkR.Triggers.RunOnce
{
    /// <summary>
    /// Extension methods for registering a <see cref="RunOnceTrigger"/>-driven worker pipeline.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a worker pipeline driven by a <see cref="RunOnceTrigger"/>. Registers
        /// <see cref="TimeProvider.System"/> if no <see cref="TimeProvider"/> is already present.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="builder">A callback that composes the worker pipeline.</param>
        /// <returns>The same <paramref name="services"/> instance.</returns>
        public static IServiceCollection AddRunOnceWorker(
            this IServiceCollection services,
            WorkerRegistration<RunOnceTrigger, EmptyTriggerContext> builder)
        {
            services.TryAddSingleton(TimeProvider.System);

            return services.AddWorker(
                sp => new RunOnceTrigger(
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<ILogger<RunOnceTrigger>>()),
                builder);
        }

        /// <summary>
        /// Registers a single terminal worker driven by a <see cref="RunOnceTrigger"/>.
        /// </summary>
        /// <typeparam name="TWorker">The worker type to run.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="workerLifetime">The lifetime to register the worker with.</param>
        /// <param name="middleware">An optional callback to configure middleware for the worker.</param>
        /// <returns>The same <paramref name="services"/> instance.</returns>
        public static IServiceCollection AddRunOnceWorker<TWorker>(
            this IServiceCollection services,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<EmptyTriggerContext>
        {
            return services.AddRunOnceWorker(
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware));
        }
    }
}
