using Microsoft.Extensions.Logging;

namespace WorkR.Triggers.Timers
{
    public sealed class DelayTrigger : ITrigger<EmptyTriggerContext>
    {
        private readonly TimeProvider _timeProvider;
        private readonly TimeSpan _delay;
        private readonly ILogger _logger;
        private readonly bool _runOnStartup;

        public DelayTrigger(
            TimeSpan delay,
            TimeProvider timeProvider,
            ILogger<DelayTrigger> logger,
            bool runOnStartup = false)
        {
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(delay.Ticks, nameof(delay));
            ArgumentNullException.ThrowIfNull(logger);

            _timeProvider = timeProvider;
            _delay = delay;
            _logger = logger;
            _runOnStartup = runOnStartup;
        }

        public async Task ExecuteAsync(WorkerDelegate<EmptyTriggerContext> workerPipeline, CancellationToken stoppingToken)
        {
            _logger.LogInformation("Delay trigger initialised with delay: {delay} and runOnStartup {runOnStartup}", _delay, _runOnStartup);

            try
            {
                if (!_runOnStartup)
                {
                    await Task.Delay(_delay, _timeProvider, stoppingToken).ConfigureAwait(false);
                }

                while (!stoppingToken.IsCancellationRequested)
                {
                    var context = new EmptyTriggerContext(_timeProvider.GetUtcNow());

                    try
                    {
                        await workerPipeline(context, stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                        when (stoppingToken.IsCancellationRequested)
                    {
                        // Expected shutdown
                    }
                    catch (Exception)
                    {
                        // Logged by WorkerService; swallow so the loop keeps running
                    }

                    await Task.Delay(_delay, _timeProvider, stoppingToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _logger.LogInformation("Delay trigger stopped");
            }
        }
    }
}
