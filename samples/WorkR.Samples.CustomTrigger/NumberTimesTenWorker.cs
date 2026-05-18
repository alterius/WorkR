namespace WorkR.Samples.CustomTrigger
{
    public class NumberTimesTenWorker : IWorker<int, int>
    {
        public Task Execute(int source, WorkerDelegate<int> next, CancellationToken ct)
        {
            return next(source * 10, ct);
        }
    }
}
