namespace WorkR
{
    internal static class TriggerPipelineTestExtensions
    {
        public static Task ExecuteAsync<TContext>(
            this ITrigger<TContext> trigger,
            Func<TContext, CancellationToken, Task> pipeline,
            CancellationToken stoppingToken)
                where TContext : TriggerContext =>
            trigger.ExecuteAsync(WorkerPipeline.Create(pipeline), stoppingToken);
    }
}
