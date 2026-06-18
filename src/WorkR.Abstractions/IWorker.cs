namespace WorkR
{
    /// <summary>
    /// A terminal worker in a pipeline. Receives a value and performs work, with no further
    /// step to pass control to.
    /// </summary>
    /// <typeparam name="TIn">
    /// The type of value this worker receives — either a <see cref="TriggerContext"/> from the
    /// trigger or the output of the preceding <see cref="IWorker{TIn, TOut}"/> in the chain.
    /// </typeparam>
    public interface IWorker<in TIn>
    {
        /// <summary>
        /// Executes the worker against the supplied value.
        /// </summary>
        /// <param name="source">The value passed in from the trigger or the previous worker.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the worker has finished its work.</returns>
        Task ExecuteAsync(TIn source, CancellationToken cancellationToken);
    }

    /// <summary>
    /// A transforming worker in a pipeline. Receives a value, performs optional work, and calls
    /// <c>next</c> to pass a new value to the following step in the chain.
    /// </summary>
    /// <typeparam name="TIn">
    /// The type of value this worker receives — either a <see cref="TriggerContext"/> from the
    /// trigger or the output of the preceding worker.
    /// </typeparam>
    /// <typeparam name="TOut">The type of value this worker passes to the next step.</typeparam>
    public interface IWorker<in TIn, out TOut>
    {
        /// <summary>
        /// Executes the worker against the supplied value and forwards a result to the next step.
        /// </summary>
        /// <param name="source">The value passed in from the trigger or the previous worker.</param>
        /// <param name="next">
        /// The continuation representing the rest of the pipeline. Invoke it with a
        /// <typeparamref name="TOut"/> value to run the next step. A worker may call it zero,
        /// one, or many times — for example, skipping the remainder of the pipeline or fanning
        /// a collection out into multiple downstream invocations.
        /// </param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A task that completes when this worker and any invoked continuations finish.</returns>
        Task ExecuteAsync(TIn source, Worker<TOut> next, CancellationToken cancellationToken);
    }
}
