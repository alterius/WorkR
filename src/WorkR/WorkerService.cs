using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WorkR
{
    internal sealed class WorkerService<TTrigger, TContext> : BackgroundService
        where TTrigger : ITrigger<TContext>
        where TContext : TriggerContext
    {
        private const string LogCategory = $"{nameof(WorkR)}.WorkerService";

        private readonly Guid _workerServiceId = Guid.NewGuid();
        private readonly TTrigger _trigger;
        private readonly IWorkerPipeline<TContext> _pipeline;
        private readonly ILogger _logger;

        public WorkerService(
            TTrigger trigger,
            IWorkerPipeline<TContext> pipeline,
            ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(trigger);
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            _trigger = trigger;
            _pipeline = pipeline;
            _logger = loggerFactory.CreateLogger(LogCategory);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var workerVersion = GetType().Assembly.GetName().Version!.ToString();
            var triggerName = TypeNameHelper.GetTypeDisplayName(typeof(TTrigger), fullName: false);
            var triggerVersion = typeof(TTrigger).Assembly.GetName().Version?.ToString() ?? "unknown";

            using var _ = _logger.BeginScope(
                new Dictionary<string, object?>
                {
                    ["WorkRVersion"] = workerVersion,
                    ["WorkerServiceId"] = _workerServiceId,
                    ["Trigger"] = triggerName,
                    ["TriggerVersion"] = triggerVersion,
                    ["WorkerPipeline"] = _pipeline.Name
                });

            _logger.LogInformation("Worker service starting...");

            var pipeline = new TelemetryWorkerPipeline<TContext>(
                _pipeline,
                _logger,
                _workerServiceId,
                workerVersion,
                triggerName,
                triggerVersion);

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
    }
}
