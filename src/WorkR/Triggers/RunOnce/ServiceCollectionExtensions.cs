using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WorkR.Middleware;

namespace WorkR.Triggers.RunOnce
{
    /// <summary>
    /// Extension methods for registering a <see cref="RunOnceTrigger"/>-driven worker pipeline
    /// that fires once when the host starts.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a worker pipeline driven by a <see cref="RunOnceTrigger"/>, composing the
        /// pipeline with the supplied configuration callback. Registers
        /// <see cref="TimeProvider.System"/> if no <see cref="TimeProvider"/> is already present.
        /// </summary>
        /// <param name="services">The service collection to add the worker service to.</param>
        /// <param name="builder">A callback that composes the worker pipeline.</param>
        /// <returns>The same <paramref name="services"/> instance, to allow chaining.</returns>
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
        /// Registers a single terminal worker driven by a <see cref="RunOnceTrigger"/>, firing
        /// it once when the host starts.
        /// </summary>
        /// <typeparam name="TWorker">The worker type to run.</typeparam>
        /// <param name="services">The service collection to add the worker service to.</param>
        /// <param name="workerLifetime">
        /// The DI lifetime to register the worker with, defaulting to
        /// <see cref="ServiceLifetime.Transient"/>.
        /// </param>
        /// <param name="middleware">An optional callback to configure middleware for the worker.</param>
        /// <returns>The same <paramref name="services"/> instance, to allow chaining.</returns>
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
