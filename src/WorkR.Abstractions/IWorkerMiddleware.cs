namespace WorkR
{
    /// <summary>
    /// Cross-cutting behaviour that wraps the execution of a worker step.
    /// </summary>
    public interface IWorkerMiddleware
    {
        /// <summary>
        /// Wraps the rest of the pipeline, performing behaviour around the call to <paramref name="next"/>.
        /// </summary>
        /// <param name="next">
        /// The continuation for the wrapped worker and any inner middleware. The token passed to
        /// it flows downstream, so a middleware may substitute a linked or modified token.
        /// </param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the middleware and the continuation finish.</returns>
        Task ExecuteAsync(Func<CancellationToken, Task> next, CancellationToken cancellationToken);
    }
}
