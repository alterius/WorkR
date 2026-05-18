namespace WorkR.Samples.CustomTrigger
{
    public class ConvertToStringWorker : IWorker<int, string>
    {
        public Task Execute(int source, WorkerDelegate<string> next, CancellationToken ct)
        {
            return next(source.ToString(), ct);
        }
    }
}
