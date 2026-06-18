namespace WorkR
{
    /// <summary>
    /// The base payload a trigger passes into the worker pipeline, carrying metadata common to
    /// every invocation.
    /// </summary>
    public abstract class TriggerContext
    {
        /// <summary>
        /// Initialises a new <see cref="TriggerContext"/> with the given execution identifier.
        /// </summary>
        /// <param name="executionId">The identifier for this execution.</param>
        /// <param name="occurredAt">The time the triggering event occurred.</param>
        public TriggerContext(Guid executionId, DateTimeOffset occurredAt)
        {
            ExecutionId = executionId;
            OccurredAt = occurredAt;
        }

        /// <summary>
        /// Initialises a new <see cref="TriggerContext"/> with a generated execution identifier.
        /// </summary>
        /// <param name="occurredAt">The time the triggering event occurred.</param>
        public TriggerContext(DateTimeOffset occurredAt)
            : this(Guid.NewGuid(), occurredAt)
        {
        }

        /// <summary>
        /// Gets the identifier for this execution, attached to its log scope and tracing span.
        /// </summary>
        public Guid ExecutionId { get; }

        /// <summary>
        /// Gets the time the triggering event occurred.
        /// </summary>
        public DateTimeOffset OccurredAt { get; }
    }
}
