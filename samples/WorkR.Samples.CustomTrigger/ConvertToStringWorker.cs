namespace WorkR.Samples.CustomTrigger
{
    public class ConvertToStringWorker : IWorker<int, string>
    {
        public Task ExecuteAsync(int source, Worker<string> next, CancellationToken cancellationToken)
        {
            return next(source.ToString(), cancellationToken);
        }
    }
}
