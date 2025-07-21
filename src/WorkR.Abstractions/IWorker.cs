namespace WorkR
{
    public interface IWorker<in TIn>
    {
        public Task Execute(TIn source, CancellationToken ct);
    }

    public interface IWorker<in TIn, out TOut>
    {
        public Task Execute(TIn source, Func<TOut, CancellationToken, Task> next, CancellationToken ct);
    }
}
