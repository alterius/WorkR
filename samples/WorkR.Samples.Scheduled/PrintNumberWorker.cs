using Microsoft.Extensions.Logging;

namespace WorkR.TestApp
{
    public class PrintNumberWorker : IWorker<int>
    {
        private readonly ILogger _logger;

        public PrintNumberWorker(ILogger<PrintNumberWorker> logger)
        {
            _logger = logger;
        }

        public Task Execute(int number, CancellationToken ct)
        {
            _logger.LogInformation("Received number: {number}", number);
            return Task.CompletedTask;
        }
    }
}
