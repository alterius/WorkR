using Microsoft.Extensions.Logging;

namespace WorkR.Middleware
{
    /// <summary>
    /// Middleware that catches and logs downstream exceptions of type
    /// <typeparamref name="TException"/>, optionally filtered by a predicate.
    /// </summary>
    /// <typeparam name="TException">The exception type to catch.</typeparam>
    public sealed class ErrorHandlingMiddleware<TException> : IWorkerMiddleware
        where TException : Exception
    {
        private readonly ILogger<ErrorHandlingMiddleware<TException>> _logger;
        private readonly Func<TException, bool>? _predicate;

        /// <summary>
        /// Initialises a new <see cref="ErrorHandlingMiddleware{TException}"/>.
        /// </summary>
        /// <param name="logger">The logger used to record handled exceptions.</param>
        /// <param name="predicate">
        /// An optional filter for a caught exception. When it returns <see langword="false"/> the
        /// exception is rethrown; when <see langword="null"/>, all matching exceptions are handled.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
        public ErrorHandlingMiddleware(ILogger<ErrorHandlingMiddleware<TException>> logger, Func<TException, bool>? predicate = null)
        {
            ArgumentNullException.ThrowIfNull(logger);

            _logger = logger;
            _predicate = predicate;
        }

        /// <inheritdoc />
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
