using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NCrontab;
using WorkR.Middleware;

namespace WorkR.Triggers.Timers
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDelayWorker(
            this IServiceCollection services,
            TimeSpan delay,
            WorkerPipelineBuilderDelegate<DelayTrigger, EmptyTriggerContext> builder)
        {
            services.TryAddSingleton(TimeProvider.System);

            return services.AddWorker(
                sp => new DelayTrigger(
                    delay,
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<ILogger<DelayTrigger>>()),
                builder,
                static mw => mw
                    .UseScope());
        }

        public static IServiceCollection AddDelayWorker<TWorker>(
            this IServiceCollection services,
            TimeSpan delay,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<EmptyTriggerContext>
        {
            return services.AddDelayWorker(
                delay,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware));
        }

        public static IServiceCollection AddScheduledWorker(
            this IServiceCollection services,
            string schedule,
            WorkerPipelineBuilderDelegate<ScheduledTrigger, EmptyTriggerContext> builder,
            bool runOnStartup = false,
            bool includeSeconds = false)
        {
            services.TryAddSingleton(TimeProvider.System);

            var cronTabSchedule = CrontabSchedule.Parse(
                schedule,
                new CrontabSchedule.ParseOptions
                {
                    IncludingSeconds = includeSeconds
                });

            return services.AddWorker(
                sp => new ScheduledTrigger(
                    cronTabSchedule,
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<ILogger<ScheduledTrigger>>(),
                    runOnStartup),
                builder,
                static mw => mw
                    .UseFireAndForget()
                    .UseScope());
        }

        public static IServiceCollection AddScheduledWorker<TWorker>(
            this IServiceCollection services,
            string schedule,
            bool runOnStartup = false,
            bool includeSeconds = false,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<EmptyTriggerContext>
        {
            return services.AddScheduledWorker(
                schedule,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware),
                runOnStartup,
                includeSeconds);
        }
    }
}
