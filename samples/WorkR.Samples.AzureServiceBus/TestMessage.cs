namespace WorkR.Samples.AzureServiceBus
{
    public record TestMessage
    {
        public required Guid ExecutionId { get; init; }
        public required DateTimeOffset OccurredAt { get; init; }
        public required string Value { get; init; }
    }
}
