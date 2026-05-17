using Microsoft.Extensions.Logging;

namespace WorkR.Triggers.RunOnce
{
    public sealed class RunOnceTrigger : ITrigger<EmptyTriggerContext>
    {
        private readonly TimeProvider _timeProvider;
        private readonly ILogger _logger;

        public RunOnceTrigger(TimeProvider timeProvider, ILogger<RunOnceTrigger> logger)
        {
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(logger);

            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Execute(WorkerDelegate<EmptyTriggerContext> next, CancellationToken stoppingToken)
        {
            _logger.LogInformation("Run once trigger executing...");

            var context = new EmptyTriggerContext(_timeProvider.GetUtcNow());
            await next(context, stoppingToken).ConfigureAwait(false);
            
            _logger.LogInformation("Run once trigger executed");
        }
    }
}
