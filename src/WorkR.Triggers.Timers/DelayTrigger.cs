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

        public async Task Execute(WorkerDelegate<EmptyTriggerContext> next, CancellationToken stoppingToken)
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

                    using var _ = _logger.BeginScope(
                        new
                        {
                            context.ExecutionId
                        });

                    _logger.LogDebug("Delay trigger executing...");

                    try
                    {
                        await next(context, stoppingToken).ConfigureAwait(false);
                        _logger.LogDebug("Delay trigger executed");
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
