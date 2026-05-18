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

        public Task Execute(EmptyTriggerContext _, CancellationToken ct)
        {
            _logger.LogInformation("Hello world!");
            return Task.CompletedTask;
        }
    }
}
