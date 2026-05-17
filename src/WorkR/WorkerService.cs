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
                new
                {
                    WorkerInstanceId = _workerInstanceId,
                    Trigger = typeof(TTrigger).Name
                });

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
