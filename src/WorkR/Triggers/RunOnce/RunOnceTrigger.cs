using Microsoft.Extensions.Logging;

namespace WorkR.Triggers.RunOnce
{
    public sealed class RunOnceTrigger : ITrigger<EmptyTriggerContext>
    {
        private readonly TimeProvider _timeProvider;
        private readonly ILogger _logger;

        public RunOnceTrigger(
            TimeProvider timeProvider,
            ILogger<RunOnceTrigger> logger)
        {
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(logger);

            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task ExecuteAsync(WorkerDelegate<EmptyTriggerContext> workerPipeline, CancellationToken stoppingToken)
        {
            _logger.LogInformation("Run once trigger executing...");

            var context = new EmptyTriggerContext(_timeProvider.GetUtcNow());

            using var _ = _logger.BeginScope(
                new Dictionary<string, object?>
                {
                    ["ExecutionId"] = context.ExecutionId
                });

            try
            {
                await workerPipeline(context, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker pipeline failed with unhandled exception");
            }
            finally
            {
                _logger.LogInformation("Run once trigger stopped");
            }
        }
    }
}
