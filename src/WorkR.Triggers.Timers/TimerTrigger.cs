using Microsoft.Extensions.Logging;
using NCrontab;

namespace WorkR.Triggers.Timers
{
    public class TimerTrigger : ITrigger<TimerSignal>
    {
        private readonly CrontabSchedule _schedule;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger _logger;
        private readonly bool _runOnStartup;

        public TimerTrigger(CrontabSchedule schedule, TimeProvider timeProvider, ILogger<TimerTrigger> logger, bool runOnStartup = false)
        {
            ArgumentNullException.ThrowIfNull(schedule);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(logger);

            _schedule = schedule;
            _timeProvider = timeProvider;
            _logger = logger;
            _runOnStartup = runOnStartup;
        }

        public async Task Execute(WorkerDelegate<TimerSignal> next, CancellationToken stoppingToken)
        {
            _logger.LogInformation("Timer trigger initialised with schedule: {schedule}", _schedule.ToString());

            async Task Next(DateTimeOffset timestamp)
            {
                _logger.LogDebug("Timer trigger executing...");

                var signal = new TimerSignal
                {
                    TriggerTimestamp = timestamp
                };

                await next(signal, stoppingToken);

                _logger.LogDebug("Timer trigger executed");
            }

            if (_runOnStartup)
            {
                await Next(_timeProvider.GetUtcNow()).ConfigureAwait(false);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
                var nextOccurrenceUtc = _schedule.GetNextOccurrence(nowUtc);
                var delay = nextOccurrenceUtc - nowUtc;

                _logger.LogInformation("Timer trigger next execution at {nextExecutionAt}", nextOccurrenceUtc);

                await Task.Delay(delay, _timeProvider, stoppingToken).ConfigureAwait(false);
                await Next(nextOccurrenceUtc).ConfigureAwait(false);
            }
        }
    }
}
