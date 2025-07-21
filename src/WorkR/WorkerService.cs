using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WorkR
{
    public sealed class WorkerService : BackgroundService
    {
        private readonly Guid _workerInstanceId = Guid.NewGuid();
        private readonly IServiceProvider _serviceProvider;
        private readonly IWorkerBuilder _pipelineBuilder;
        private readonly ILogger _logger;

        public WorkerService(
            IServiceProvider serviceProvider,
            IWorkerBuilder pipelineBuilder,
            ILogger<WorkerService> logger)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);
            ArgumentNullException.ThrowIfNull(pipelineBuilder);
            ArgumentNullException.ThrowIfNull(logger);

            _serviceProvider = serviceProvider;
            _pipelineBuilder = pipelineBuilder;
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

                var pipeline = _pipelineBuilder.Build(_serviceProvider);

                _logger.LogInformation("Worker started");

                try
                {
                    await pipeline(stoppingToken).ConfigureAwait(false);
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
