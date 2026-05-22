namespace WorkR.Triggers.AzureStorageQueues
{
    public static class StorageQueueDelayStrategy
    {
        /// <summary>
        /// Returns the same delay regardless of count.
        /// </summary>
        public static StorageQueueDelay Fixed(TimeSpan delay)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(delay, TimeSpan.Zero);
            return _ => delay;
        }

        /// <summary>
        /// Increases the delay linearly with each count, up to a maximum.
        /// </summary>
        public static StorageQueueDelay Linear(TimeSpan initial, TimeSpan step, TimeSpan max)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(initial, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(step, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(max, TimeSpan.Zero);

            return count =>
            {
                // Guard against overflow: if step * count would push past max, return max directly.
                var remainingTicks = max.Ticks - initial.Ticks;
                if (remainingTicks <= 0 || count >= remainingTicks / step.Ticks)
                {
                    return max;
                }

                var delay = initial + step * count;
                return delay < max ? delay : max;
            };
        }

        /// <summary>
        /// Doubles the delay with each count, up to a maximum.
        /// </summary>
        public static StorageQueueDelay Exponential(TimeSpan initial, TimeSpan max)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(initial, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(max, TimeSpan.Zero);

            return count =>
            {
                // Guard against overflow: 2^count overflows long at count >= 63.
                // Use bit-shifting; if it would exceed max, return max directly.
                if (count >= 63 || (initial.Ticks << count) >= max.Ticks)
                {
                    return max;
                }

                return TimeSpan.FromTicks(initial.Ticks << count);
            };
        }
    }
}
