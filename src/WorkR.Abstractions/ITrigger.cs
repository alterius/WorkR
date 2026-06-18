namespace WorkR
{
    /// <summary>
    /// Owns the execution loop that drives a worker pipeline.
    /// </summary>
    /// <remarks>
    /// A trigger runs for the lifetime of the host and is resolved once, so implementations
    /// should be safe to use as a singleton.
    /// </remarks>
    /// <typeparam name="TContext">The context type passed into the worker pipeline.</typeparam>
    public interface ITrigger<out TContext>
        where TContext : TriggerContext
    {
        /// <summary>
        /// Runs the execution loop, invoking <paramref name="workerPipeline"/> for each occurrence.
        /// </summary>
        /// <param name="workerPipeline">The pipeline to run with a newly created context per occurrence.</param>
        /// <param name="stoppingToken">A token signalled when the host is shutting down.</param>
        /// <returns>A task that completes when the execution loop ends.</returns>
        Task ExecuteAsync(IWorkerPipeline<TContext> workerPipeline, CancellationToken stoppingToken);
    }
}
