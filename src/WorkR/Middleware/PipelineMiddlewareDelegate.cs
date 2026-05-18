namespace WorkR.Middleware
{
    internal delegate Task PipelineMiddlewareDelegate(IServiceProvider sp, CancellationToken ct);
}
