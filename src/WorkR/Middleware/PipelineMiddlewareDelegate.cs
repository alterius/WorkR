namespace WorkR.Middleware
{
    internal delegate Task PipelineMiddlewareDelegate(IServiceProvider serviceProvider, CancellationToken cancellationToken);
}
