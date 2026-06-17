namespace WorkR
{
    internal static class TriggerPipelineTestExtensions
    {
        public static Task ExecuteAsync<TContext>(
            this ITrigger<TContext> trigger,
            Func<TContext, CancellationToken, Task> pipeline,
            CancellationToken stoppingToken)
                where TContext : TriggerContext =>
            trigger.ExecuteAsync(new DelegatePipeline<TContext>(pipeline), stoppingToken);

        private sealed class DelegatePipeline<TIn> : IWorkerPipeline<TIn>
        {
            private readonly Func<TIn, CancellationToken, Task> _pipeline;

            public DelegatePipeline(Func<TIn, CancellationToken, Task> pipeline) =>
                _pipeline = pipeline;

            public string Name => "TestPipeline";

            public Task ExecuteAsync(TIn value, CancellationToken cancellationToken) =>
                _pipeline(value, cancellationToken);
        }
    }
}
