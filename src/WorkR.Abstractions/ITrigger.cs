namespace WorkR
{
    public interface ITrigger<out TOut>
    {
        Task Execute(WorkerDelegate<TOut> next, CancellationToken stoppingToken);
    }
}
