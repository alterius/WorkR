namespace WorkR
{
    public interface IWorker<in TIn>
    {
        Task Execute(TIn source, CancellationToken ct);
    }

    public interface IWorker<in TIn, out TOut>
    {
        Task Execute(TIn source, WorkerDelegate<TOut> next, CancellationToken ct);
    }
}
