namespace WorkR
{
    public interface ITrigger<out TContext>
        where TContext : TriggerContext
    {
        Task ExecuteAsync(WorkerPipeline<TContext> workerPipeline, CancellationToken stoppingToken);
    }
}
