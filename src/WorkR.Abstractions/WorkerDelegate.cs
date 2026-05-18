namespace WorkR
{
    public delegate Task WorkerDelegate<in TOut>(TOut value, CancellationToken ct);
}
