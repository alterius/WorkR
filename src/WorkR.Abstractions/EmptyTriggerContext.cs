namespace WorkR
{
    /// <summary>
    /// A <see cref="TriggerContext"/> with no payload, carrying only timing metadata.
    /// </summary>
    public sealed class EmptyTriggerContext : TriggerContext
    {
        /// <summary>
        /// Initialises a new <see cref="EmptyTriggerContext"/> with a generated execution identifier.
        /// </summary>
        /// <param name="occurredAt">The time the triggering event occurred.</param>
        public EmptyTriggerContext(DateTimeOffset occurredAt)
            : base(occurredAt)
        {
        }
    }
}
