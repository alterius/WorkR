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

        public async Task ExecuteAsync(IWorkerPipeline<EmptyTriggerContext> workerPipeline, CancellationToken stoppingToken)
        {
            _logger.LogInformation("Run once trigger initialised");

            var context = new EmptyTriggerContext(_timeProvider.GetUtcNow());

            try
            {
                await workerPipeline.ExecuteAsync(context, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                // Expected shutdown
            }
            catch (Exception)
            {
                // Logged by WorkerService; swallow
            }
            finally
            {
                _logger.LogInformation("Run once trigger stopped");
            }
        }
    }
}
