namespace WorkR
{
    public interface IWorkerPipeline
    {
        Task ExecuteAsync(ExecutionContext context, CancellationToken stoppingToken);
    }
}
