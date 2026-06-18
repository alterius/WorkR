using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace WorkR
{
    /// <summary>
    /// Decorates a worker pipeline with telemetry: a tracing span per execution and debug/error
    /// logging around the inner pipeline.
    /// </summary>
    /// <remarks>
    /// Cancellation triggered by the stopping token is treated as expected shutdown and logged at
    /// debug; other exceptions mark the span as failed and are logged as errors before rethrowing.
    /// </remarks>
    internal sealed class TelemetryWorkerPipeline<TContext> : IWorkerPipeline<TContext>
        where TContext : TriggerContext
    {
        private readonly IWorkerPipeline<TContext> _inner;
        private readonly ILogger _logger;

        private readonly Guid _workerServiceId;
        private readonly string _workerVersion;
        private readonly string _triggerName;
        private readonly string _triggerVersion;
        private readonly string _pipelineName;
        private readonly string _spanName;

        internal TelemetryWorkerPipeline(
            INamedWorkerPipeline<TContext> inner,
            ILogger logger,
            Guid workerServiceId,
            string workerVersion,
            string triggerName,
            string triggerVersion)
        {
            ArgumentNullException.ThrowIfNull(inner);
            ArgumentNullException.ThrowIfNull(logger);

            _inner = inner;
            _logger = logger;
            _workerServiceId = workerServiceId;
            _workerVersion = workerVersion;
            _triggerName = triggerName;
            _triggerVersion = triggerVersion;
            _pipelineName = inner.Name;
            _spanName = $"EXECUTE {inner.Name}";
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
                new LogScope("ExecutionId", value.ExecutionId));

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

        /// <summary>
        /// Records an exception on the activity, using the native API on .NET 9+ and an
        /// equivalent event on earlier targets.
        /// </summary>
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
