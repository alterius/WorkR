using Microsoft.Extensions.Logging;

namespace WorkR.Middleware
{
    public class FireAndForgetMiddleware : IWorkerMiddleware
    {
        private readonly ILogger _logger;

        public FireAndForgetMiddleware(ILogger<FireAndForgetMiddleware> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            _logger = logger;
        }

        public Task Execute(Func<CancellationToken, Task> next, CancellationToken ct)
        {
            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await next(ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                        when (ct.IsCancellationRequested)
                    {
                        // Expected shutdown
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Worker execution failed with unhandled exception");
                    }
                },
                ct).ConfigureAwait(false);
            return Task.CompletedTask;
        }
    }
}
