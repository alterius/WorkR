namespace WorkR.Middleware
{
    internal interface IPipelineMiddleware
    {
        Task Execute(IServiceProvider sp, PipelineMiddlewareDelegate next, CancellationToken ct);
    }
}
