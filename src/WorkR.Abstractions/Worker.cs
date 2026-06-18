namespace WorkR
{
    /// <summary>
    /// The continuation passed to an <see cref="IWorker{TIn, TOut}"/>; invoking it runs the next
    /// step of the pipeline.
    /// </summary>
    /// <typeparam name="TIn">The type of value passed to the next step.</typeparam>
    /// <param name="value">The value to forward to the next step.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the downstream pipeline finishes.</returns>
    public delegate Task Worker<in TIn>(TIn value, CancellationToken cancellationToken);
}
