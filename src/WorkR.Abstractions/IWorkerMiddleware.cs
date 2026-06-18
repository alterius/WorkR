namespace WorkR
{
    /// <summary>
    /// Cross-cutting behaviour that wraps the execution of a worker step, such as error
    /// handling, timeouts, logging, or tracing. Middleware is configured per worker step and
    /// applied in registration order, with the first-registered middleware outermost.
    /// </summary>
    public interface IWorkerMiddleware
    {
        /// <summary>
        /// Wraps the rest of the pipeline. Implementations perform their behaviour around the
        /// call to <paramref name="next"/>, which executes the wrapped worker (and any inner
        /// middleware).
        /// </summary>
        /// <param name="next">
        /// The continuation representing the wrapped worker and any inner middleware. Invoke it
        /// to proceed; the token passed to it flows downstream, so middleware may substitute a
        /// linked or modified token (for example, to apply a timeout).
        /// </param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the middleware and the continuation finish.</returns>
        Task ExecuteAsync(Func<CancellationToken, Task> next, CancellationToken cancellationToken);
    }
}
