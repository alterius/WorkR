namespace WorkR.Triggers.AzureStorageQueues
{
    public class StorageQueueTriggerConfig
    {
        public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(5);
        public TimeSpan VisibilityTimeout { get; init; } = TimeSpan.FromSeconds(30);
        public int MaxMessages { get; init; } = 1;
    }
}
