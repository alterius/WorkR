namespace WorkR
{
    public interface IMiddleware
    {
        Task Execute(Func<CancellationToken, Task> next, CancellationToken ct);
    }
}
