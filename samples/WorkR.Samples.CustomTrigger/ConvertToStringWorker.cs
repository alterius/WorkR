namespace WorkR.Samples.CustomTrigger
{
    public class ConvertToStringWorker : IWorker<int, string>
    {
        public Task ExecuteAsync(int source, WorkerPipeline<string> next, CancellationToken cancellationToken)
        {
            return next(source.ToString(), cancellationToken);
        }
    }
}
