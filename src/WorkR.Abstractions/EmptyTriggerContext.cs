namespace WorkR
{
    /// <summary>
    /// A <see cref="TriggerContext"/> with no payload, used by triggers that carry only timing
    /// metadata — such as delay, scheduled, and run-once triggers.
    /// </summary>
    public sealed class EmptyTriggerContext : TriggerContext
    {
        /// <summary>
        /// Initialises a new <see cref="EmptyTriggerContext"/> with a freshly generated
        /// execution identifier.
        /// </summary>
        /// <param name="occurredAt">The time at which the triggering event occurred.</param>
        public EmptyTriggerContext(DateTimeOffset occurredAt)
            : base(occurredAt)
        {
        }
    }
}
