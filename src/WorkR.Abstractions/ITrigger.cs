namespace WorkR
{
    public interface ITrigger<out TOut>
    {
        Task Execute(Func<TOut, CancellationToken, Task> next, CancellationToken stoppingToken);
    }
}
