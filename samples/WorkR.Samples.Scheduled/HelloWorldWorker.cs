using Microsoft.Extensions.Logging;
using WorkR.Triggers.Timers;

namespace WorkR.TestApp
{
    public class HelloWorldWorker : IWorker<TimerSignal>
    {
        private readonly ILogger _logger;

        public HelloWorldWorker(ILogger<HelloWorldWorker> logger)
        {
            _logger = logger;
        }

        public Task Execute(TimerSignal _, CancellationToken ct)
        {
            _logger.LogInformation("Hello world!");
            return Task.CompletedTask;
        }
    }
}
