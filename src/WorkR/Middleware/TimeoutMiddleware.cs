namespace WorkR.Middleware
{
    internal sealed class TimeoutMiddleware : IWorkerMiddleware
    {
        private readonly TimeProvider _timeProvider;
        private readonly TimeSpan _timeout;

        public TimeoutMiddleware(TimeProvider timeProvider, TimeSpan timeout)
        {
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeout.Ticks, nameof(timeout));

            _timeProvider = timeProvider;
            _timeout = timeout;
        }

        public async Task ExecuteAsync(Func<CancellationToken, Task> next, CancellationToken cancellationToken)
        {
            using var timeoutCts = new CancellationTokenSource(_timeout, _timeProvider);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await next(cts.Token).ConfigureAwait(false);
        }
    }
}
