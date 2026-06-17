namespace WorkR
{
    public interface IWorker<in TIn>
    {
        Task ExecuteAsync(TIn source, CancellationToken cancellationToken);
    }

    public interface IWorker<in TIn, out TOut>
    {
        Task ExecuteAsync(TIn source, WorkerPipeline<TOut> next, CancellationToken cancellationToken);
    }
}
