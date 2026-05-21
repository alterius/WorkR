using Microsoft.Extensions.Logging;
using NCrontab;

namespace WorkR.Triggers.Timers
{
    public sealed class ScheduledTrigger : ITrigger<EmptyTriggerContext>
    {
        private readonly CrontabSchedule _schedule;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger _logger;
        private readonly bool _runOnStartup;

        public ScheduledTrigger(
            CrontabSchedule schedule,
            TimeProvider timeProvider,
            ILogger<ScheduledTrigger> logger,
            bool runOnStartup = false)
        {
            ArgumentNullException.ThrowIfNull(schedule);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(logger);

            _schedule = schedule;
            _timeProvider = timeProvider;
            _logger = logger;
            _runOnStartup = runOnStartup;
        }

        public async Task Execute(WorkerDelegate<EmptyTriggerContext> next, CancellationToken stoppingToken)
        {
            _logger.LogInformation("Scheduled trigger initialised with schedule {schedule} and runOnStartup {runOnStartup}", _schedule.ToString(), _runOnStartup);

            async Task Next(DateTimeOffset timestamp)
            {
                var context = new EmptyTriggerContext(timestamp);

                using var _ = _logger.BeginScope(
                    new
                    {
                        context.ExecutionId
                    });

                _logger.LogDebug("Scheduled trigger executing...");

                try
                {
                    await next(context, stoppingToken).ConfigureAwait(false);
                    _logger.LogDebug("Scheduled trigger executed");
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
            }

            try
            {
                if (_runOnStartup)
                {
                    await Next(_timeProvider.GetUtcNow()).ConfigureAwait(false);
                }

                while (!stoppingToken.IsCancellationRequested)
                {
                    var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
                    var nextOccurrenceUtc = _schedule.GetNextOccurrence(nowUtc);
                    var delay = TimeSpan.FromTicks(Math.Max((nextOccurrenceUtc - nowUtc).Ticks, 0));

                    _logger.LogDebug("Scheduled trigger next execution at {nextExecutionAt}", nextOccurrenceUtc);

                    await Task.Delay(delay, _timeProvider, stoppingToken).ConfigureAwait(false);
                    await Next(nextOccurrenceUtc).ConfigureAwait(false);
                }
            }
            finally
            {
                _logger.LogInformation("Scheduled trigger stopped");
            }
        }
    }
}
