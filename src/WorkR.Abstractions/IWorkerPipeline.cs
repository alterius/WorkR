namespace WorkR
{
    /// <summary>
    /// Represents a composed worker pipeline that a trigger invokes to run the worker chain
    /// once for a given input value.
    /// </summary>
    /// <typeparam name="TIn">
    /// The type of value the pipeline accepts, typically the trigger's
    /// <see cref="TriggerContext"/>.
    /// </typeparam>
    public interface IWorkerPipeline<in TIn>
    {
        /// <summary>
        /// Runs the worker pipeline once for the supplied value.
        /// </summary>
        /// <param name="value">The input value to feed into the first worker in the chain.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the pipeline execution finishes.</returns>
        Task ExecuteAsync(TIn value, CancellationToken cancellationToken);
    }
}
