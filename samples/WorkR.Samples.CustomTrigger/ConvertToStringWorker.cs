namespace WorkR.Samples.CustomTrigger
{
    public class ConvertToStringWorker : IWorker<int, string>
    {
        public Task ExecuteAsync(int source, IWorkerPipeline<string> next, CancellationToken cancellationToken)
        {
            return next.ExecuteAsync(source.ToString(), cancellationToken);
        }
    }
}
