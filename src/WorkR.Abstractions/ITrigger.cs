namespace WorkR
{
    public interface ITrigger<out TContext>
        where TContext : TriggerContext
    {
        Task ExecuteAsync(WorkerDelegate<TContext> workerPipeline, CancellationToken stoppingToken);
    }
}
