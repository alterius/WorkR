namespace WorkR.Middleware
{
    internal interface IInternalWorkerMiddleware
    {
        Task ExecuteAsync(IServiceProvider sp, WorkerMiddleware next, CancellationToken cancellationToken);
    }
}
