namespace WorkR
{
    public interface IWorkerBuilder
    {
        Func<CancellationToken, Task> Build(IServiceProvider serviceProvider);
    }
}
