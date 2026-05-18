namespace WorkR
{
    public abstract class TriggerContext
    {
        public TriggerContext(DateTimeOffset occurredAt)
        {
            ExecutionId = Guid.NewGuid();
            OccurredAt = occurredAt;
        }

        public Guid ExecutionId { get; }
        public DateTimeOffset OccurredAt { get; }
    }
}
