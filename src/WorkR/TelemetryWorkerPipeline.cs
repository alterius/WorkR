using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace WorkR
{
    internal sealed class TelemetryWorkerPipeline<TContext> : IWorkerPipeline<TContext>
        where TContext : TriggerContext
    {
        private readonly IWorkerPipeline<TContext> _inner;
        private readonly ILogger _logger;
        private readonly string _spanName;
        private readonly string _workerVersion;
        private readonly Guid _workerServiceId;
        private readonly string _triggerName;
        private readonly string _triggerVersion;
        private readonly string _pipelineName;

        internal TelemetryWorkerPipeline(
            IWorkerPipeline<TContext> inner,
            ILogger logger,
            Guid workerServiceId,
            string workerVersion,
            string triggerName,
            string triggerVersion,
            string pipelineName)
        {
            ArgumentNullException.ThrowIfNull(inner);
            ArgumentNullException.ThrowIfNull(logger);

            _inner = inner;
            _logger = logger;
            _workerServiceId = workerServiceId;
            _workerVersion = workerVersion;
            _triggerName = triggerName;
            _triggerVersion = triggerVersion;
            _pipelineName = pipelineName;
            _spanName = $"EXECUTE {pipelineName}";
        }

        public async Task ExecuteAsync(TContext value, CancellationToken cancellationToken)
        {
            using var activity = WorkRDiagnostics.Source.StartActivity(_spanName, ActivityKind.Internal);

            if (activity?.IsAllDataRequested ?? false)
            {
                activity.SetTag("workr.version", _workerVersion);
                activity.SetTag("workr.service.id", _workerServiceId);
                activity.SetTag("workr.trigger", _triggerName);
                activity.SetTag("workr.trigger.version", _triggerVersion);
                activity.SetTag("workr.pipeline", _pipelineName);
                activity.SetTag("workr.execution.id", value.ExecutionId);
            }

            using var _ = _logger.BeginScope(
                new Dictionary<string, object?>
                {
                    ["ExecutionId"] = value.ExecutionId
                });

            _logger.LogDebug("Worker pipeline executing...");

            try
            {
                await _inner.ExecuteAsync(value, cancellationToken).ConfigureAwait(false);

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
