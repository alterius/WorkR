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
            _logger.LogInformation("Run once trigger started");

            var context = new EmptyTriggerContext(_timeProvider.GetUtcNow());

            try
            {
                await workerPipeline(context, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Logged by WorkerService; swallow so the host keeps running
            }
            finally
            {
                _logger.LogInformation("Run once trigger stopped");
            }
        }
    }
}
