using System.Security.Cryptography;

namespace WorkR.Samples.CustomTrigger
{
    public class RandomNumberTrigger : ITrigger<ValueTriggerContext<int>>
    {
        private readonly TimeProvider _timeProvider;

        public RandomNumberTrigger(TimeProvider timeProvider)
        {
            ArgumentNullException.ThrowIfNull(timeProvider);
            _timeProvider = timeProvider;
        }

        public async Task Execute(WorkerDelegate<ValueTriggerContext<int>> next, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var context = new ValueTriggerContext<int>(
                    _timeProvider.GetUtcNow(),
                    RandomNumberGenerator.GetInt32(100));

                await next(context, ct).ConfigureAwait(false);

                var delay = RandomNumberGenerator.GetInt32(1000, 10000);
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
    }
}
