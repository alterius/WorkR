using Microsoft.Extensions.Logging;

namespace WorkR.Triggers.RunOnce
{
    /// <summary>
    /// A trigger that fires its worker pipeline once when the host starts, then stops.
    /// </summary>
    public sealed class RunOnceTrigger : ITrigger<EmptyTriggerContext>
    {
        private readonly TimeProvider _timeProvider;
        private readonly ILogger _logger;

        /// <summary>
        /// Initialises a new <see cref="RunOnceTrigger"/>.
        /// </summary>
        /// <param name="timeProvider">The time provider used to stamp the context's occurrence time.</param>
        /// <param name="logger">The logger used to record the trigger's lifecycle.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="timeProvider"/> or <paramref name="logger"/> is <see langword="null"/>.
        /// </exception>
        public RunOnceTrigger(
            TimeProvider timeProvider,
            ILogger<RunOnceTrigger> logger)
        {
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(logger);

            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <inheritdoc />
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
