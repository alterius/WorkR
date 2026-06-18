using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WorkR
{
    /// <summary>
    /// The <see cref="BackgroundService"/> host that runs a trigger and its worker pipeline,
    /// establishing the per-service log scope and wrapping the pipeline with telemetry.
    /// </summary>
    internal sealed class WorkerService<TTrigger, TContext> : BackgroundService
        where TTrigger : ITrigger<TContext>
        where TContext : TriggerContext
    {
        private const string LogCategory = $"{nameof(WorkR)}.WorkerService";

        private readonly Guid _workerServiceId = Guid.NewGuid();
        private readonly TTrigger _trigger;
        private readonly INamedWorkerPipeline<TContext> _pipeline;
        private readonly ILogger _logger;

        public WorkerService(
            TTrigger trigger,
            INamedWorkerPipeline<TContext> pipeline,
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

            using var _ = _logger.BeginScope(new LogScope(
                new("WorkRVersion", workerVersion),
                new("WorkerServiceId", _workerServiceId),
                new("Trigger", triggerName),
                new("TriggerVersion", triggerVersion),
                new("WorkerPipeline", _pipeline.Name)));

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
