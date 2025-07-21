namespace WorkR.Middleware
{
    internal interface IServiceProviderMiddleware
    {
        Task Execute(IServiceProvider sp, ServiceProviderMiddlewareDelegate next, CancellationToken ct);
    }
}
