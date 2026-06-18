namespace WorkR
{
    /// <summary>
    /// A worker pipeline that exposes its composed name for logging and tracing.
    /// </summary>
    internal interface INamedWorkerPipeline<in TIn> : IWorkerPipeline<TIn>
    {
        /// <summary>
        /// Gets the pipeline's name, formed by joining its worker names with <c>" -&gt; "</c>.
        /// </summary>
        string Name { get; }
    }
}
