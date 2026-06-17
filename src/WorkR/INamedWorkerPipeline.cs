namespace WorkR
{
    internal interface INamedWorkerPipeline<in TIn> : IWorkerPipeline<TIn>
    {
        string Name { get; }
    }
}
