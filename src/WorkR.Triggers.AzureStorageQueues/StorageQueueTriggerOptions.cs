namespace WorkR.Triggers.AzureStorageQueues
{
    public sealed class StorageQueueTriggerOptions
    {
        /// <summary>
        /// Determines how long to wait between polls when the queue is empty.
        /// Receives the number of consecutive empty polls, resetting to zero when messages are received.
        /// Defaults to a fixed 5-second delay.
        /// </summary>
        public StorageQueueDelay PollingDelay { get; set; } = StorageQueueDelayStrategy.Fixed(TimeSpan.FromSeconds(5));

        /// <summary>
        /// Determines how long to wait after a transient error before retrying.
        /// Receives the number of consecutive errors, resetting to zero on a successful receive.
        /// Defaults to a fixed 5-second delay.
        /// </summary>
        public StorageQueueDelay ErrorDelay { get; set; } = StorageQueueDelayStrategy.Fixed(TimeSpan.FromSeconds(5));

        /// <summary>
        /// How long a received message remains invisible to other consumers while being processed.
        /// Defaults to 30 seconds.
        /// </summary>
        public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The maximum number of messages to retrieve per poll. Must be between 1 and 32.
        /// Defaults to 1.
        /// </summary>
        public int MaxMessages { get; set; } = 1;

        /// <summary>
        /// When true, messages are automatically deleted from the queue after successful processing.
        /// Defaults to true.
        /// </summary>
        public bool AutoCompleteMessages { get; set; } = true;

        /// <summary>
        /// The number of times a message may be dequeued and fail before being dead-lettered.
        /// Set to 0 to disable automatic dead-lettering.
        /// Defaults to 5.
        /// </summary>
        public int MaxDeliveryCount { get; set; } = 5;

        /// <summary>
        /// The maximum number of worker executions that may run concurrently.
        /// The polling loop applies backpressure once this limit is reached.
        /// Defaults to 1.
        /// </summary>
        public int MaxConcurrentCalls { get; set; } = 1;
    }
}
