namespace WorkR
{
    /// <summary>
    /// The base type for the payload a trigger passes into the worker pipeline. Carries
    /// metadata common to every pipeline invocation and is extended by triggers that need to
    /// carry additional data.
    /// </summary>
    public abstract class TriggerContext
    {
        /// <summary>
        /// Initialises a new <see cref="TriggerContext"/> with an explicit execution identifier.
        /// </summary>
        /// <param name="executionId">A unique identifier for this individual pipeline execution.</param>
        /// <param name="occurredAt">The time at which the triggering event occurred.</param>
        public TriggerContext(Guid executionId, DateTimeOffset occurredAt)
        {
            ExecutionId = executionId;
            OccurredAt = occurredAt;
        }

        /// <summary>
        /// Initialises a new <see cref="TriggerContext"/> with a freshly generated
        /// <see cref="ExecutionId"/>.
        /// </summary>
        /// <param name="occurredAt">The time at which the triggering event occurred.</param>
        public TriggerContext(DateTimeOffset occurredAt)
            : this(Guid.NewGuid(), occurredAt)
        {
        }

        /// <summary>
        /// Gets the unique identifier for this individual pipeline execution. It is attached to
        /// the execution's log scope and tracing span and can be used to correlate telemetry.
        /// </summary>
        public Guid ExecutionId { get; }

        /// <summary>
        /// Gets the time at which the triggering event occurred.
        /// </summary>
        public DateTimeOffset OccurredAt { get; }
    }
}
