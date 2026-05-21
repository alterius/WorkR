using Microsoft.Extensions.Logging;

namespace WorkR.Middleware
{
    public sealed class FireAndForgetMiddleware : IWorkerMiddleware
    {
        private readonly ILogger _logger;

        public FireAndForgetMiddleware(ILogger<FireAndForgetMiddleware> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            _logger = logger;
        }

        public Task ExecuteAsync(Func<CancellationToken, Task> next, CancellationToken cancellationToken)
        {
            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await next(cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                        when (cancellationToken.IsCancellationRequested)
                    {
                        // Expected shutdown
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Worker execution failed with unhandled exception");
                    }
                },
                cancellationToken);
            return Task.CompletedTask;
        }
    }
}
