namespace WorkR
{
    public class DelayPipeline : IWorkerPipeline
    {
        private readonly IWorkerPipeline _innerPipeline;
        private readonly TimeProvider _timeProvider;
        private readonly TimeSpan _delay;

        public DelayPipeline(IWorkerPipeline innerPipeline, TimeProvider timeProvider, TimeSpan delay)
        {
            _innerPipeline = innerPipeline ?? throw new ArgumentNullException(nameof(innerPipeline));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            _delay = delay;
        }

        public async Task ExecuteAsync(ExecutionContext context, CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await _innerPipeline.ExecuteAsync(context, stoppingToken);
                await Task.Delay(_delay, _timeProvider, stoppingToken);
            }
        }
    }
}
