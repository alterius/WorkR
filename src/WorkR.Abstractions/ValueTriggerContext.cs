namespace WorkR
{
    /// <summary>
    /// A <see cref="TriggerContext"/> that carries a single typed value.
    /// </summary>
    /// <typeparam name="T">The type of the carried value.</typeparam>
    public class ValueTriggerContext<T> : TriggerContext
    {
        /// <summary>
        /// Initialises a new <see cref="ValueTriggerContext{T}"/> with the given execution identifier.
        /// </summary>
        /// <param name="executionId">The identifier for this execution.</param>
        /// <param name="occurredAt">The time the triggering event occurred.</param>
        /// <param name="value">The carried value.</param>
        public ValueTriggerContext(Guid executionId, DateTimeOffset occurredAt, T value)
            : base(executionId, occurredAt)
        {
            Value = value;
        }

        /// <summary>
        /// Initialises a new <see cref="ValueTriggerContext{T}"/> with a generated execution identifier.
        /// </summary>
        /// <param name="occurredAt">The time the triggering event occurred.</param>
        /// <param name="value">The carried value.</param>
        public ValueTriggerContext(DateTimeOffset occurredAt, T value)
            : base(occurredAt)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the carried value.
        /// </summary>
        public T Value { get; }
    }
}
