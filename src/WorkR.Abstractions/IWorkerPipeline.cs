namespace WorkR
{
    public interface IWorkerPipeline<in TIn>
    {
        Task ExecuteAsync(TIn value, CancellationToken cancellationToken);
    }
}
