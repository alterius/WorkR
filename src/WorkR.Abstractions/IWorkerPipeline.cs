namespace WorkR
{
    /// <summary>
    /// A composed worker pipeline that a trigger invokes to run the worker chain once.
    /// </summary>
    /// <typeparam name="TIn">The type of value the pipeline accepts.</typeparam>
    public interface IWorkerPipeline<in TIn>
    {
        /// <summary>
        /// Runs the pipeline once for the supplied value.
        /// </summary>
        /// <param name="value">The value fed into the first worker in the chain.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the pipeline execution finishes.</returns>
        Task ExecuteAsync(TIn value, CancellationToken cancellationToken);
    }
}
