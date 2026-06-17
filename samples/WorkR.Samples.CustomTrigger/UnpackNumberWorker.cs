namespace WorkR.Samples.CustomTrigger
{
    public class UnpackNumberWorker : IWorker<ValueTriggerContext<int>, int>
    {
        public Task ExecuteAsync(ValueTriggerContext<int> source, IWorkerPipeline<int> next, CancellationToken cancellationToken)
        {
            return next.ExecuteAsync(source.Value, cancellationToken);
        }
    }
}
