namespace WorkR.Triggers.AzureStorageQueues
{
    /// <summary>
    /// Returns a delay duration given a consecutive count.
    /// For idle polling, the count is the number of consecutive empty polls, resetting to zero when messages are received.
    /// For error delays, the count is the number of consecutive errors, resetting to zero on a successful receive.
    /// </summary>
    public delegate TimeSpan StorageQueueDelay(int count);
}
