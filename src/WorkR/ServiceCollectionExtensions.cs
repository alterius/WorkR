using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WorkR
{
    /// <summary>
    /// Extension methods for registering WorkR workers and triggers with an
    /// <see cref="IServiceCollection"/>.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a hosted worker service driven by the specified trigger, building its
        /// worker pipeline with the supplied configuration callback.
        /// </summary>
        /// <typeparam name="TTrigger">The trigger type that owns the execution loop.</typeparam>
        /// <typeparam name="TContext">The context type the trigger produces.</typeparam>
        /// <param name="services">The service collection to add the worker service to.</param>
        /// <param name="triggerFactory">
        /// A factory that creates the trigger instance from the application's service provider.
        /// Invoked once when the hosted service is constructed.
        /// </param>
        /// <param name="builder">
        /// A callback that composes the worker pipeline using the supplied
        /// <see cref="WorkerRegistrationBuilder{TTrigger, TContext}"/>.
        /// </param>
        /// <returns>The same <paramref name="services"/> instance, to allow chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="triggerFactory"/> or <paramref name="builder"/> is <see langword="null"/>.
        /// </exception>
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
