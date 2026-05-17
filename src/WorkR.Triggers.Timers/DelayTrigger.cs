using Microsoft.Extensions.Logging;

namespace WorkR.Triggers.Timers
{
    public sealed class DelayTrigger : ITrigger<EmptyTriggerContext>
    {
        private readonly TimeProvider _timeProvider;
        private readonly TimeSpan _delay;
        private readonly ILogger _logger;

        public DelayTrigger(TimeProvider timeProvider, TimeSpan delay, ILogger<DelayTrigger> logger)
        {
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(delay.Ticks, nameof(delay));
            ArgumentNullException.ThrowIfNull(logger);

            _timeProvider = timeProvider;
            _delay = delay;
            _logger = logger;
        }

        public async Task Execute(WorkerDelegate<EmptyTriggerContext> next, CancellationToken stoppingToken)
        {
            _logger.LogInformation("Delay trigger initialised with delay: {delay}", _delay);

            while (!stoppingToken.IsCancellationRequested)
            {
                var context = new EmptyTriggerContext(_timeProvider.GetUtcNow());

                _logger.LogDebug("Delay trigger executing...");

                await next(context, stoppingToken).ConfigureAwait(false);

                _logger.LogDebug("Delay trigger executed");

                await Task.Delay(_delay, _timeProvider, stoppingToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Delay trigger completed");
        }
    }
}
