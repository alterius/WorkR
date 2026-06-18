using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WorkR.Middleware;

namespace WorkR.Triggers.Timers
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDelayWorker(
            this IServiceCollection services,
            TimeSpan delay,
            WorkerRegistration<DelayTrigger, EmptyTriggerContext> builder,
            bool runOnStartup = false)
        {
            services.TryAddSingleton(TimeProvider.System);

            return services.AddWorker(
                sp => new DelayTrigger(
                    delay,
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<ILogger<DelayTrigger>>(),
                    runOnStartup),
                builder);
        }

        public static IServiceCollection AddDelayWorker<TWorker>(
            this IServiceCollection services,
            TimeSpan delay,
            bool runOnStartup = false,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<EmptyTriggerContext>
        {
            return services.AddDelayWorker(
                delay,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware),
                runOnStartup);
        }

        public static IServiceCollection AddScheduledWorker(
            this IServiceCollection services,
            string schedule,
            WorkerRegistration<ScheduledTrigger, EmptyTriggerContext> builder,
            bool includeSeconds = false,
            bool runOnStartup = false,
            bool cancelOnOverlap = false)
        {
            var cronTabSchedule = ScheduledTrigger.ParseSchedule(schedule, includeSeconds);

            services.TryAddSingleton(TimeProvider.System);

            return services.AddWorker(
                sp => new ScheduledTrigger(
                    cronTabSchedule,
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<ILogger<ScheduledTrigger>>(),
                    runOnStartup,
                    cancelOnOverlap),
                builder);
        }

        public static IServiceCollection AddScheduledWorker<TWorker>(
            this IServiceCollection services,
            string schedule,
            bool includeSeconds = false,
            bool runOnStartup = false,
            bool cancelOnOverlap = false,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<EmptyTriggerContext>
        {
            return services.AddScheduledWorker(
                schedule,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware),
                includeSeconds,
                runOnStartup,
                cancelOnOverlap);
        }
    }
}
