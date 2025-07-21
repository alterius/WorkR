using Microsoft.Extensions.Logging;

namespace WorkR.TestApp
{
    public class HelloWorldWorker : Worker
    {
        private readonly ILogger _logger;

        public HelloWorldWorker(WorkerPipelineBuilder pipelineBuilder, ILogger<HelloWorldWorker> logger)
            : base(pipelineBuilder)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override Task ExecuteAsync(ExecutionContext context, CancellationToken stoppingToken)
        {
            _logger.LogInformation("Hello world!");
            return Task.CompletedTask;
        }
    }
}
