namespace WorkR.Triggers.AzureStorageQueues
{
    public sealed class StorageQueueTriggerOptions
    {
        /// <summary>
        /// Determines how long to wait between polls when the queue is empty.
        /// Receives the number of consecutive empty polls, resetting to zero when messages are received.
        /// Defaults to a fixed 5-second delay.
        /// </summary>
        public StorageQueueDelay PollingDelay { get; init; } = StorageQueueDelayStrategy.Fixed(TimeSpan.FromSeconds(5));

        /// <summary>
        /// Determines how long to wait after a transient error before retrying.
        /// Receives the number of consecutive errors, resetting to zero on a successful receive.
        /// Defaults to a fixed 5-second delay.
        /// </summary>
        public StorageQueueDelay ErrorDelay { get; init; } = StorageQueueDelayStrategy.Fixed(TimeSpan.FromSeconds(5));

        /// <summary>
        /// How long a received message remains invisible to other consumers while being processed.
        /// Defaults to 30 seconds.
        /// </summary>
        public TimeSpan VisibilityTimeout { get; init; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The maximum number of messages to retrieve per poll. Must be between 1 and 32.
        /// Defaults to 1.
        /// </summary>
        public int MaxMessages { get; init; } = 1;
    }
}
