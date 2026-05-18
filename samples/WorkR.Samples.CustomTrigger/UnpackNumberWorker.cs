namespace WorkR.Samples.CustomTrigger
{
    public class UnpackNumberWorker : IWorker<ValueTriggerContext<int>, int>
    {
        public Task Execute(ValueTriggerContext<int> source, WorkerDelegate<int> next, CancellationToken ct)
        {
            return next(source.Value, ct);
        }
    }
}
