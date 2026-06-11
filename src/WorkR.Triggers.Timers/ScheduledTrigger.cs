using System.Collections.Concurrent;
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
        private readonly bool _cancelOnOverlap;

        public ScheduledTrigger(
            CrontabSchedule schedule,
            TimeProvider timeProvider,
            ILogger<ScheduledTrigger> logger,
            bool runOnStartup = false,
            bool cancelOnOverlap = false)
        {
            ArgumentNullException.ThrowIfNull(schedule);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(logger);

            _schedule = schedule;
            _timeProvider = timeProvider;
            _logger = logger;
            _runOnStartup = runOnStartup;
            _cancelOnOverlap = cancelOnOverlap;
        }

        public async Task ExecuteAsync(WorkerDelegate<EmptyTriggerContext> workerPipeline, CancellationToken stoppingToken)
        {
            _logger.LogInformation("Scheduled trigger initialised with schedule {schedule} and runOnStartup {runOnStartup}", _schedule.ToString(), _runOnStartup);

            CancellationTokenSource? previousExecutionCts = null;
            var executingTasks = new ConcurrentDictionary<Guid, Task>();

            async Task Next(Guid executionId, DateTimeOffset timestamp, CancellationToken executionToken)
            {
                var context = new EmptyTriggerContext(timestamp);

                using var _ = _logger.BeginScope(new Dictionary<string, object?> { ["ExecutionId"] = context.ExecutionId });

                _logger.LogDebug("Scheduled trigger executing...");

                try
                {
                    await workerPipeline(context, executionToken).ConfigureAwait(false);
                    _logger.LogDebug("Scheduled trigger executed");
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    // Expected shutdown
                }
                catch (OperationCanceledException)
                    when (executionToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Scheduled trigger execution cancelled due to overlap");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Worker pipeline failed with unhandled exception");
                }
                finally
                {
                    executingTasks.TryRemove(executionId, out var __);
                }
            }

            CancellationToken NextExecutionToken()
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                previousExecutionCts?.Cancel();
                previousExecutionCts?.Dispose();
                previousExecutionCts = cts;
                return cts.Token;
            }

            void Fire(DateTimeOffset timestamp)
            {
                var executionId = Guid.NewGuid();
                var token = _cancelOnOverlap ? NextExecutionToken() : stoppingToken;
                executingTasks[executionId] = Task.Run(() => Next(executionId, timestamp, token), stoppingToken);
            }

            try
            {
                if (_runOnStartup)
                {
                    Fire(_timeProvider.GetUtcNow());
                }

                while (!stoppingToken.IsCancellationRequested)
                {
                    var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
                    var nextOccurrenceUtc = _schedule.GetNextOccurrence(nowUtc);
                    var delay = TimeSpan.FromTicks(Math.Max((nextOccurrenceUtc - nowUtc).Ticks, 0));

                    _logger.LogDebug("Scheduled trigger next execution at {nextExecutionAt}", nextOccurrenceUtc);

                    await Task.Delay(delay, _timeProvider, stoppingToken).ConfigureAwait(false);

                    Fire(nextOccurrenceUtc);
                }
            }
            finally
            {
                previousExecutionCts?.Cancel();
                previousExecutionCts?.Dispose();

                try
                {
                    await Task.WhenAll(executingTasks.Values).ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                _logger.LogInformation("Scheduled trigger stopped");
            }
        }
    }
}
