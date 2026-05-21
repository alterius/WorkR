namespace WorkR
{
    public interface IWorkerMiddleware
    {
        Task ExecuteAsync(Func<CancellationToken, Task> next, CancellationToken cancellationToken);
    }
}
