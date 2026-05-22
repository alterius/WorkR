namespace WorkR
{
    public class ValueTriggerContext<T> : TriggerContext
    {
        public ValueTriggerContext(Guid executionId, DateTimeOffset occurredAt, T value)
            : base(executionId, occurredAt)
        {
            Value = value;
        }

        public ValueTriggerContext(DateTimeOffset occurredAt, T value)
            : base(occurredAt)
        {
            Value = value;
        }

        public T Value { get; }
    }
}
