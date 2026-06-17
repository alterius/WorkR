namespace WorkR
{
    public interface ITrigger<out TContext>
        where TContext : TriggerContext
    {
        Task ExecuteAsync(IWorkerPipeline<TContext> workerPipeline, CancellationToken stoppingToken);
    }
}
