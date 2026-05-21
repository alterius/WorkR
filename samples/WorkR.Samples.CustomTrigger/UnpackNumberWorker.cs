namespace WorkR.Samples.CustomTrigger
{
    public class UnpackNumberWorker : IWorker<ValueTriggerContext<int>, int>
    {
        public Task ExecuteAsync(ValueTriggerContext<int> source, WorkerDelegate<int> next, CancellationToken cancellationToken)
        {
            return next(source.Value, cancellationToken);
        }
    }
}
