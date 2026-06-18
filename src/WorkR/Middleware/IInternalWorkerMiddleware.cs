namespace WorkR.Middleware
{
    /// <summary>
    /// Middleware with access to the service provider, allowing it to substitute the provider
    /// passed to <paramref name="next"/> (for example, to introduce a new scope).
    /// </summary>
    internal interface IInternalWorkerMiddleware
    {
        /// <summary>
        /// Wraps <paramref name="next"/>, optionally passing it a different service provider.
        /// </summary>
        /// <param name="sp">The current service provider.</param>
        /// <param name="next">The continuation for the wrapped worker and inner middleware.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the middleware and continuation finish.</returns>
        Task ExecuteAsync(IServiceProvider sp, WorkerMiddleware next, CancellationToken cancellationToken);
    }
}
