namespace WorkR.Tests
{
    /// <summary>
    /// A configurable <see cref="ITrigger{TContext}"/> for <see cref="EmptyTriggerContext"/>
    /// that delegates execution to the supplied callback (no-op by default).
    /// </summary>
    internal sealed class FakeTrigger : ITrigger<EmptyTriggerContext>
    {
        private readonly Func<IWorkerPipeline<EmptyTriggerContext>, CancellationToken, Task> _execute;

        public FakeTrigger(
            Func<IWorkerPipeline<EmptyTriggerContext>, CancellationToken, Task>? execute = null)
        {
            _execute = execute ?? ((_, _) => Task.CompletedTask);
        }

        public Task ExecuteAsync(IWorkerPipeline<EmptyTriggerContext> workerPipeline, CancellationToken stoppingToken) =>
            _execute(workerPipeline, stoppingToken);
    }
}
