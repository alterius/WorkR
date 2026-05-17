namespace WorkR
{
    public interface IWorkerMiddleware
    {
        Task Execute(Func<CancellationToken, Task> next, CancellationToken ct);
    }
}
