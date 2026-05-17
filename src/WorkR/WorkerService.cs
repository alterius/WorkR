using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WorkR
{
    public sealed class WorkerService<TTrigger, TTriggerOut> : BackgroundService
        where TTrigger : ITrigger<TTriggerOut>
    {
        private readonly Guid _workerInstanceId = Guid.NewGuid();
        private readonly IServiceProvider _serviceProvider;
        private readonly TTrigger _trigger;
        private readonly WorkerPipeline<TTriggerOut> _workerPipeline;
        private readonly ILogger _logger;

        public WorkerService(
            IServiceProvider serviceProvider,
            TTrigger trigger,
            WorkerPipeline<TTriggerOut> workerPipeline,
            ILogger<WorkerService<TTrigger, TTriggerOut>> logger)
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
            using (_logger.BeginScope(
                new
                {
                    WorkerInstanceId = _workerInstanceId,
                }))
            {
                _logger.LogInformation("Worker starting...");

                var pipeline = _workerPipeline.Build(_serviceProvider);

                _logger.LogInformation("Worker started");

                try
                {
                    await _trigger.Execute(pipeline, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Worker shutting down...");
                }

                _logger.LogInformation("Worker stopped");
            }
        }
    }
}
