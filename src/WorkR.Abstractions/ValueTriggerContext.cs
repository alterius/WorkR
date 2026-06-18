namespace WorkR
{
    /// <summary>
    /// A <see cref="TriggerContext"/> that carries a single typed value, suitable for triggers
    /// whose payload is one item such as a queue message body.
    /// </summary>
    /// <typeparam name="T">The type of the carried value.</typeparam>
    public class ValueTriggerContext<T> : TriggerContext
    {
        /// <summary>
        /// Initialises a new <see cref="ValueTriggerContext{T}"/> with an explicit execution
        /// identifier.
        /// </summary>
        /// <param name="executionId">A unique identifier for this individual pipeline execution.</param>
        /// <param name="occurredAt">The time at which the triggering event occurred.</param>
        /// <param name="value">The value carried by this context.</param>
        public ValueTriggerContext(Guid executionId, DateTimeOffset occurredAt, T value)
            : base(executionId, occurredAt)
        {
            Value = value;
        }

        /// <summary>
        /// Initialises a new <see cref="ValueTriggerContext{T}"/> with a freshly generated
        /// execution identifier.
        /// </summary>
        /// <param name="occurredAt">The time at which the triggering event occurred.</param>
        /// <param name="value">The value carried by this context.</param>
        public ValueTriggerContext(DateTimeOffset occurredAt, T value)
            : base(occurredAt)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the value carried by this context.
        /// </summary>
        public T Value { get; }
    }
}
