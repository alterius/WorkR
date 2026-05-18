namespace WorkR
{
    public interface ITrigger<out TContext>
        where TContext : TriggerContext
    {
        Task Execute(WorkerDelegate<TContext> next, CancellationToken stoppingToken);
    }
}
