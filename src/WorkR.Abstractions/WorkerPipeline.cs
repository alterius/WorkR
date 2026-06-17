namespace WorkR
{
    public delegate Task WorkerPipeline<in TIn>(TIn value, CancellationToken cancellationToken);
}
