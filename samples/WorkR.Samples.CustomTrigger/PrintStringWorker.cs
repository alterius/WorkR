using Microsoft.Extensions.Logging;

namespace WorkR.Samples.CustomTrigger
{
    public class PrintStringWorker : IWorker<string>
    {
        private readonly ILogger<PrintStringWorker> _logger;

        public PrintStringWorker(ILogger<PrintStringWorker> logger)
        {
            _logger = logger;
        }

        public Task Execute(string source, CancellationToken ct)
        {
            _logger.LogInformation("I'm printing a string: {string}", source);
            return Task.CompletedTask;
        }
    }
}
