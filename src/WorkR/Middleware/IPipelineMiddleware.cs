namespace WorkR.Middleware
{
    internal interface IPipelineMiddleware
    {
        Task ExecuteAsync(IServiceProvider sp, PipelineMiddlewareDelegate next, CancellationToken cancellationToken);
    }
}
