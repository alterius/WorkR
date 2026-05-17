using Microsoft.Extensions.Logging;

namespace WorkR.TestApp
{
    public class NumberTimesTenWorker : IWorker<int, int>
    {
        public async Task Execute(int source, WorkerDelegate<int> next, CancellationToken ct)
        {
            await next(source * 10, ct);
        }
    }

    public class ConvertToStringWorker : IWorker<int, string>
    {
        public async Task Execute(int source, WorkerDelegate<string> next, CancellationToken ct)
        {
            await next(source.ToString(), ct);
        }
    }

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
