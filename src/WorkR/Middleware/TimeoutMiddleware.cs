namespace WorkR.Middleware
{
    /// <summary>
    /// Middleware that cancels downstream execution if it runs longer than a specified timeout.
    /// </summary>
    /// <remarks>
    /// A timeout token is linked with the incoming cancellation token, and the combined token is
    /// passed downstream.
    /// </remarks>
    public sealed class TimeoutMiddleware : IWorkerMiddleware
    {
        private readonly TimeProvider _timeProvider;
        private readonly TimeSpan _timeout;

        /// <summary>
        /// Initialises a new <see cref="TimeoutMiddleware"/>.
        /// </summary>
        /// <param name="timeProvider">The time provider used to schedule the timeout.</param>
        /// <param name="timeout">The maximum duration before execution is cancelled. Must be positive.</param>
        /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is zero or negative.</exception>
        public TimeoutMiddleware(TimeProvider timeProvider, TimeSpan timeout)
        {
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeout.Ticks, nameof(timeout));

            _timeProvider = timeProvider;
            _timeout = timeout;
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(Func<CancellationToken, Task> next, CancellationToken cancellationToken)
        {
            using var timeoutCts = new CancellationTokenSource(_timeout, _timeProvider);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await next(cts.Token).ConfigureAwait(false);
        }
    }
}
