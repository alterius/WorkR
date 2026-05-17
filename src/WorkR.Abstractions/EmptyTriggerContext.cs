namespace WorkR
{
    public sealed class EmptyTriggerContext : TriggerContext
    {
        public EmptyTriggerContext(DateTimeOffset occurredAt)
            : base(occurredAt)
        {
        }
    }
}
