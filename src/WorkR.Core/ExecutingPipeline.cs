namespace WorkR
{
    public class ExecutingPipeline : IWorkerPipeline
    {
        private readonly Func<ExecutionContext, CancellationToken, Task> _execute;

        public ExecutingPipeline(Func<ExecutionContext, CancellationToken, Task> execute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public async Task ExecuteAsync(ExecutionContext context, CancellationToken stoppingToken)
        {
            await _execute(context, stoppingToken);
        }
    }
}
