using Microsoft.Extensions.Logging;

namespace WorkR.Middleware
{
    public class ErrorHandlingMiddleware<TException> : IWorkerMiddleware
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

        public async Task Execute(Func<CancellationToken, Task> next, CancellationToken ct)
        {
            try
            {
                await next(ct).ConfigureAwait(false);
            }
            catch (TException ex)
                when (_predicate?.Invoke(ex) ?? true)
            {
                _logger.LogError(ex, "Worker execution failed with unhandled exception");
            }
        }
    }
}
