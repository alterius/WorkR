using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCrontab;
using WorkR.Middleware;

namespace WorkR.Triggers.Timers
{
    public static class Extensions
    {
        public static IServiceCollection AddDelayWorker(
            this IServiceCollection services,
            TimeSpan delay,
            WorkerPipelineBuilderDelegate<DelayTrigger, TimerSignal> builder)
        {
            services.TryAddSingleton(TimeProvider.System);

            return services.AddWorker(
                sp => ActivatorUtilities.CreateInstance<DelayTrigger>(sp, delay),
                builder,
                static mw => mw
                    .UseScope()
                    .UseErrorHandling<Exception>(ex => ex is not OperationCanceledException));
        }

        public static IServiceCollection AddDelayWorker<TWorker>(
            this IServiceCollection services,
            TimeSpan delay,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TimerSignal>
        {
            return services.AddDelayWorker(
                delay,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware));
        }

        public static IServiceCollection AddScheduledWorker(
            this IServiceCollection services,
            string schedule,
            WorkerPipelineBuilderDelegate<TimerTrigger, TimerSignal> builder,
            bool runOnStartup = false,
            CrontabSchedule.ParseOptions? parseOptions = null)
        {
            services.TryAddSingleton(TimeProvider.System);

            var cronTabSchedule = CrontabSchedule.Parse(
                schedule,
                parseOptions ?? new CrontabSchedule.ParseOptions
                {
                    IncludingSeconds = false
                });

            return services.AddWorker(
                sp => ActivatorUtilities.CreateInstance<TimerTrigger>(sp, cronTabSchedule, runOnStartup),
                builder,
                static mw => mw
                    .UseFireAndForget()
                    .UseScope()
                    .UseErrorHandling<Exception>(ex => ex is not OperationCanceledException));
        }

        public static IServiceCollection AddScheduledWorker<TWorker>(
            this IServiceCollection services,
            string schedule,
            bool runOnStartup = false,
            CrontabSchedule.ParseOptions? parseOptions = null,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TimerSignal>
        {
            return services.AddScheduledWorker(
                schedule,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware),
                runOnStartup,
                parseOptions);
        }
    }
}
