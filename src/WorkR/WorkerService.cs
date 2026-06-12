using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WorkR
{
    public sealed class WorkerService<TTrigger, TContext> : BackgroundService
        where TTrigger : ITrigger<TContext>
        where TContext : TriggerContext
    {
        private readonly Guid _workerServiceId = Guid.NewGuid();
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
            var workerVersion = GetType().Assembly.GetName().Version!.ToString();
            var triggerName = TypeNameHelper.GetTypeDisplayName(typeof(TTrigger), fullName: false);
            var triggerVersion = typeof(TTrigger).Assembly.GetName().Version?.ToString() ?? "unknown";
            var pipelineName = string.Join(" -> ", _workerPipeline.WorkerTypes.Select(t => TypeNameHelper.GetTypeDisplayName(t, fullName: false)));

            using var _ = _logger.BeginScope(
                new Dictionary<string, object?>
                {
                    ["WorkRVersion"] = workerVersion,
                    ["WorkerServiceId"] = _workerServiceId,
                    ["Trigger"] = triggerName,
                    ["TriggerVersion"] = triggerVersion,
                    ["WorkerPipeline"] = pipelineName
                });

            _logger.LogInformation("Worker service starting...");

            var pipeline = WithTelemetry(
                _workerPipeline.Build(_serviceProvider),
                workerVersion,
                triggerName,
                triggerVersion,
                pipelineName);

            _logger.LogInformation("Worker service started");

            try
            {
                await _trigger.ExecuteAsync(pipeline, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker service shutting down...");
            }

            _logger.LogInformation("Worker service stopped");
        }

        private WorkerDelegate<TContext> WithTelemetry(
            WorkerDelegate<TContext> pipeline,
            string workerVersion,
            string triggerName,
            string triggerVersion,
            string pipelineName)
        {
            var spanName = $"EXECUTE {pipelineName}";

            return async (context, cancellationToken) =>
            {
                using var activity = WorkRDiagnostics.Source.StartActivity(spanName, ActivityKind.Internal);

                if (activity?.IsAllDataRequested ?? false)
                {
                    activity.SetTag("workr.version", workerVersion);
                    activity.SetTag("workr.service.id", _workerServiceId);
                    activity.SetTag("workr.trigger", triggerName);
                    activity.SetTag("workr.trigger.version", triggerVersion);
                    activity.SetTag("workr.pipeline", pipelineName);
                    activity.SetTag("workr.execution.id", context.ExecutionId);
                }

                using var _ = _logger.BeginScope(
                    new Dictionary<string, object?>
                    {
                        ["ExecutionId"] = context.ExecutionId
                    });

                _logger.LogDebug("Worker pipeline executing...");

                try
                {
                    await pipeline(context, cancellationToken).ConfigureAwait(false);

                    _logger.LogDebug("Worker pipeline executed");
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Worker pipeline execution cancelled");

                    throw;
                }
                catch (Exception ex)
                {
                    if (activity?.IsAllDataRequested ?? false)
                    {
                        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
                        activity.SetTag("error.type", ex.GetType().FullName);
                        AddException(activity, ex);
                    }

                    _logger.LogError(ex, "Worker pipeline execution failed");

                    throw;
                }
            };
        }

        private static void AddException(Activity activity, Exception ex)
        {
#if NET9_0_OR_GREATER
            activity.AddException(ex);
#else
            activity.AddEvent(new ActivityEvent(
                "exception",
                tags: new ActivityTagsCollection
                {
                    ["exception.type"] = ex.GetType().ToString(),
                    ["exception.message"] = ex.Message,
                    ["exception.stacktrace"] = ex.ToString()
                }));
#endif
        }
    }
}
