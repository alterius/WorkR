namespace WorkR
{
    public interface IWorkerPipeline<in TIn>
    {
        string Name { get; }
        Task ExecuteAsync(TIn value, CancellationToken cancellationToken);
    }
}
