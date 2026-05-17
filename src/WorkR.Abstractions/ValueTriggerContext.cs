namespace WorkR
{
    public class ValueTriggerContext<T> : TriggerContext
    {
        public ValueTriggerContext(DateTimeOffset occurredAt, T value)
            : base(occurredAt)
        {
            Value = value;
        }

        public T Value { get; }
    }
}
