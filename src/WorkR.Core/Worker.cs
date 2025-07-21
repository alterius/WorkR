using Microsoft.Extensions.Hosting;

namespace WorkR
{
    public abstract class Worker : BackgroundService
    {
        private readonly IWorkerPipeline _pipeline;

        protected Worker(WorkerPipelineBuilder pipelineBuilder)
        {
            ArgumentNullException.ThrowIfNull(pipelineBuilder);

            _pipeline = pipelineBuilder.Build(ExecuteAsync);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var context = new ExecutionContext
            {
                Instance = Guid.NewGuid()
            };

            await _pipeline.ExecuteAsync(context, stoppingToken);
        }

        protected abstract Task ExecuteAsync(ExecutionContext context, CancellationToken stoppingToken);
    }
}
