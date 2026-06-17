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
        private readonly IServiceProvider _serviceProvider;
        private readonly TTrigger _trigger;
        private readonly WorkerPipelineBuilder<TContext> _pipelineBuilder;
        private readonly ILogger _logger;

        public WorkerService(
            IServiceProvider serviceProvider,
            TTrigger trigger,
            WorkerPipelineBuilder<TContext> pipelineBuilder,
            ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);
            ArgumentNullException.ThrowIfNull(trigger);
            ArgumentNullException.ThrowIfNull(pipelineBuilder);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            _serviceProvider = serviceProvider;
            _trigger = trigger;
            _pipelineBuilder = pipelineBuilder;
            _logger = loggerFactory.CreateLogger(LogCategory);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var workerVersion = GetType().Assembly.GetName().Version!.ToString();
            var triggerName = TypeNameHelper.GetTypeDisplayName(typeof(TTrigger), fullName: false);
            var triggerVersion = typeof(TTrigger).Assembly.GetName().Version?.ToString() ?? "unknown";
            var pipelineName = string.Join(" -> ", _pipelineBuilder.WorkerNames);

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

            var pipeline = new TelemetryWorkerPipeline<TContext>(
                _pipelineBuilder.Build(_serviceProvider),
                _logger,
                _workerServiceId,
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
    }
}
