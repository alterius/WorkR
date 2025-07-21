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
            WorkerRegistrationBuilderDelegate<DelayTrigger, TimerSignal> builder)
        {
            services.TryAddSingleton(TimeProvider.System);

            return services.AddWorker<DelayTrigger, TimerSignal>(
                sp => ActivatorUtilities.CreateInstance<DelayTrigger>(sp, delay),
                b => builder(b),
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
                builder => builder.RegisterWorker<TWorker>(workerLifetime, middleware));
        }

        public static IServiceCollection AddScheduledWorker(
            this IServiceCollection services,
            string schedule,
            WorkerRegistrationBuilderDelegate<TimerTrigger, TimerSignal> builder,
            bool runOnStartup = false,
            CrontabSchedule.ParseOptions? parseOptions = null)
        {
            services.TryAddSingleton(TimeProvider.System);

            var cronTabSchedule = CrontabSchedule.Parse(
                schedule,
                parseOptions ?? new CrontabSchedule.ParseOptions
                {
                    IncludingSeconds = true
                });

            return services.AddWorker<TimerTrigger, TimerSignal>(
                sp => ActivatorUtilities.CreateInstance<TimerTrigger>(sp, cronTabSchedule, runOnStartup),
                b => builder(b),
                static mw => mw
                    .UseFireAndForget()
                    .UseScope()
                    .UseErrorHandling<Exception>(ex => ex is not OperationCanceledException));
        }

        public static IServiceCollection AddScheduledWorker<TWorker>(
            this IServiceCollection services,
            string schedule,
            bool runOnStartup = false,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TimerSignal>
        {
            return services.AddScheduledWorker(
                schedule,
                builder => builder.RegisterWorker<TWorker>(workerLifetime, middleware),
                runOnStartup);
        }
    }
}
