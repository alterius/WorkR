using Microsoft.Extensions.Logging;

namespace WorkR.Samples.Scheduled
{
    public class HelloWorldWorker : IWorker<EmptyTriggerContext>
    {
        private readonly ILogger _logger;

        public HelloWorldWorker(ILogger<HelloWorldWorker> logger)
        {
            _logger = logger;
        }

        public Task ExecuteAsync(EmptyTriggerContext _, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Hello world!");
            return Task.CompletedTask;
        }
    }
}
