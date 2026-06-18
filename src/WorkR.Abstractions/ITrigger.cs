namespace WorkR
{
    /// <summary>
    /// Owns the execution loop for a worker pipeline. A trigger decides <em>when</em> the
    /// pipeline runs — on a timer, a queue message, a schedule, or any other signal — and
    /// invokes the pipeline for each occurrence.
    /// </summary>
    /// <remarks>
    /// Triggers are long-lived; their <see cref="ExecuteAsync"/> method runs for the lifetime
    /// of the host and typically contains the polling, waiting, or scheduling logic. A trigger
    /// is resolved once and reused, so implementations should be safe to run as a singleton.
    /// </remarks>
    /// <typeparam name="TContext">
    /// The type of <see cref="TriggerContext"/> this trigger produces and passes into the
    /// worker pipeline.
    /// </typeparam>
    public interface ITrigger<out TContext>
        where TContext : TriggerContext
    {
        /// <summary>
        /// Runs the trigger's execution loop, invoking <paramref name="workerPipeline"/> for
        /// each occurrence until the host shuts down.
        /// </summary>
        /// <param name="workerPipeline">
        /// The downstream worker pipeline. Call
        /// <see cref="IWorkerPipeline{TIn}.ExecuteAsync"/> with a freshly created
        /// <typeparamref name="TContext"/> to run the worker chain once.
        /// </param>
        /// <param name="stoppingToken">
        /// A token that is signalled when the host is shutting down. Implementations should
        /// observe it to stop the loop promptly.
        /// </param>
        /// <returns>A task that completes when the trigger's execution loop ends.</returns>
        Task ExecuteAsync(IWorkerPipeline<TContext> workerPipeline, CancellationToken stoppingToken);
    }
}
