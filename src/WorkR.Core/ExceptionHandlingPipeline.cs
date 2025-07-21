namespace WorkR
{
    public class ExceptionHandlingPipeline : IWorkerPipeline
    {
        private readonly IWorkerPipeline _innerPipeline;
        private readonly Func<ExecutionContext, Exception, Task>? _onExceptionThrown;

        public ExceptionHandlingPipeline(IWorkerPipeline innerPipeline, Func<ExecutionContext, Exception, Task>? onExceptionThrown = null)
        {
            _innerPipeline = innerPipeline ?? throw new ArgumentNullException(nameof(innerPipeline));
            _onExceptionThrown = onExceptionThrown;
        }

        public async Task ExecuteAsync(ExecutionContext context, CancellationToken stoppingToken)
        {
            try
            {
                await _innerPipeline.ExecuteAsync(context, stoppingToken);
            }
            catch (Exception ex)
            {
                if (_onExceptionThrown is not null)
                {
                    await _onExceptionThrown(context, ex);
                }
            }
        }
    }
}
