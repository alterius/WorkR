namespace WorkR
{
    public delegate Task Worker<in TIn>(TIn value, CancellationToken cancellationToken);
}
