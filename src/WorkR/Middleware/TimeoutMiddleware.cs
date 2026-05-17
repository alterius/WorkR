namespace WorkR.Middleware
{
    public class TimeoutMiddleware : IWorkerMiddleware
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

        public async Task Execute(Func<CancellationToken, Task> next, CancellationToken ct)
        {
            using var timeoutCts = new CancellationTokenSource(_timeout, _timeProvider);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            await next(cts.Token).ConfigureAwait(false);
        }
    }
}
