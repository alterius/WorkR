namespace WorkR
{
    public abstract class TriggerContext
    {
        public TriggerContext(Guid executionId, DateTimeOffset occurredAt)
        {
            ExecutionId = executionId;
            OccurredAt = occurredAt;
        }

        public TriggerContext(DateTimeOffset occurredAt)
            : this(Guid.NewGuid(), occurredAt)
        {
        }

        public Guid ExecutionId { get; }
        public DateTimeOffset OccurredAt { get; }
    }
}
