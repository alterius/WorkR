using Microsoft.Extensions.Logging;

namespace WorkR.Middleware
{
    internal sealed class ErrorHandlingMiddleware<TException> : IWorkerMiddleware
        where TException : Exception
    {
        private readonly ILogger<ErrorHandlingMiddleware<TException>> _logger;
        private readonly Func<TException, bool>? _predicate;

        public ErrorHandlingMiddleware(ILogger<ErrorHandlingMiddleware<TException>> logger, Func<TException, bool>? predicate = null)
        {
            ArgumentNullException.ThrowIfNull(logger);

            _logger = logger;
            _predicate = predicate;
        }

        public async Task ExecuteAsync(Func<CancellationToken, Task> next, CancellationToken cancellationToken)
        {
            try
            {
                await next(cancellationToken).ConfigureAwait(false);
            }
            catch (TException ex)
                when (_predicate?.Invoke(ex) ?? true)
            {
                _logger.LogError(ex, "Worker execution failed with unhandled exception");
            }
        }
    }
}
