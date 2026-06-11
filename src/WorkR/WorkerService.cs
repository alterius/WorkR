using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WorkR
{
    public sealed class WorkerService<TTrigger, TContext> : BackgroundService
        where TTrigger : ITrigger<TContext>
        where TContext : TriggerContext
    {
        private readonly Guid _workerInstanceId = Guid.NewGuid();
        private readonly IServiceProvider _serviceProvider;
        private readonly TTrigger _trigger;
        private readonly WorkerPipeline<TContext> _workerPipeline;
        private readonly ILogger _logger;

        public WorkerService(
            IServiceProvider serviceProvider,
            TTrigger trigger,
            WorkerPipeline<TContext> workerPipeline,
            ILogger<WorkerService<TTrigger, TContext>> logger)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);
            ArgumentNullException.ThrowIfNull(trigger);
            ArgumentNullException.ThrowIfNull(workerPipeline);
            ArgumentNullException.ThrowIfNull(logger);

            _serviceProvider = serviceProvider;
            _trigger = trigger;
            _workerPipeline = workerPipeline;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var _ = _logger.BeginScope(
                new Dictionary<string, object?>
                {
                    ["WorkerInstanceId"] = _workerInstanceId,
                    ["Trigger"] = typeof(TTrigger).Name
                });

            _logger.LogInformation("Worker starting...");

            var pipeline = WithTracing(_workerPipeline.Build(_serviceProvider));

            _logger.LogInformation("Worker started");

            try
            {
                await _trigger.ExecuteAsync(pipeline, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker shutting down...");
            }

            _logger.LogInformation("Worker stopped");
        }

        private static WorkerDelegate<TContext> WithTracing(WorkerDelegate<TContext> pipeline)
        {
            var spanName = CleanTypeName(typeof(TContext).Name);
            var triggerName = CleanTypeName(typeof(TTrigger).Name);

            return async (context, cancellationToken) =>
            {
                // No explicit parent: picks up Activity.Current ambiently
                // (e.g. a messaging SDK's process span) or becomes a trace root.
                using var activity = WorkRDiagnostics.Source.StartActivity(spanName);

                activity?.SetTag("workr.execution.id", context.ExecutionId);
                activity?.SetTag("workr.trigger", triggerName);

                try
                {
                    await pipeline(context, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
#if NET9_0_OR_GREATER
                    activity?.AddException(ex);
#else
                    activity?.AddEvent(new ActivityEvent(
                        "exception",
                        tags: new ActivityTagsCollection
                        {
                            ["exception.type"] = ex.GetType().ToString(),
                            ["exception.message"] = ex.Message,
                            ["exception.stacktrace"] = ex.ToString()
                        }));
#endif
                    throw;
                }
            };
        }

        private static string CleanTypeName(string name)
        {
            var backtick = name.IndexOf('`');
            return backtick < 0 ? name : name[..backtick];
        }
    }
}
