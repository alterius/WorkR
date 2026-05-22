namespace WorkR.Samples.CustomTrigger
{
    public class NumberTimesTenWorker : IWorker<int, int>
    {
        public Task ExecuteAsync(int source, WorkerDelegate<int> next, CancellationToken cancellationToken)
        {
            return next(source * 10, cancellationToken);
        }
    }
}
