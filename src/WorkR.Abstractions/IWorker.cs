namespace WorkR
{
    /// <summary>
    /// A terminal worker in a pipeline. Receives a value and performs work.
    /// </summary>
    /// <typeparam name="TIn">The type of value the worker receives.</typeparam>
    public interface IWorker<in TIn>
    {
        /// <summary>
        /// Executes the worker against the supplied value.
        /// </summary>
        /// <param name="source">The value from the trigger or the previous worker.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the worker finishes.</returns>
        Task ExecuteAsync(TIn source, CancellationToken cancellationToken);
    }

    /// <summary>
    /// A transforming worker in a pipeline. Receives a value and forwards a result to the next step.
    /// </summary>
    /// <typeparam name="TIn">The type of value the worker receives.</typeparam>
    /// <typeparam name="TOut">The type of value the worker forwards to the next step.</typeparam>
    public interface IWorker<in TIn, out TOut>
    {
        /// <summary>
        /// Executes the worker against the supplied value and forwards a result via <paramref name="next"/>.
        /// </summary>
        /// <param name="source">The value from the trigger or the previous worker.</param>
        /// <param name="next">
        /// The continuation for the rest of the pipeline. May be invoked any number of times.
        /// </param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the worker and any invoked continuations finish.</returns>
        Task ExecuteAsync(TIn source, Worker<TOut> next, CancellationToken cancellationToken);
    }
}
