namespace WorkR.Middleware
{
    internal delegate Task ServiceProviderMiddlewareDelegate(IServiceProvider sp, CancellationToken ct);
}
