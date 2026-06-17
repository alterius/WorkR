namespace WorkR.Samples.CustomTrigger
{
    public class NumberTimesTenWorker : IWorker<int, int>
    {
        public Task ExecuteAsync(int source, IWorkerPipeline<int> next, CancellationToken cancellationToken)
        {
            return next.ExecuteAsync(source * 10, cancellationToken);
        }
    }
}
