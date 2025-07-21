using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;
using WorkR.Middleware;

namespace WorkR.Tests.Middleware
{
    public class ErrorHandlingMiddlewareTests
    {
        [Fact]
        public async Task Execute_WhenNoExceptionThrown_CallsNext()
        {
            var called = false;
            var middleware = Create<Exception>();

            await middleware.Execute(ct => { called = true; return Task.CompletedTask; }, CancellationToken.None);

            called.ShouldBeTrue();
        }

        [Fact]
        public async Task Execute_WhenMatchingExceptionTypeThrown_SwallowsException()
        {
            var middleware = Create<InvalidOperationException>();

            await Should.NotThrowAsync(() =>
                middleware.Execute(_ => Task.FromException(new InvalidOperationException()), CancellationToken.None));
        }

        [Fact]
        public async Task Execute_WhenNonMatchingExceptionTypeThrown_PropagatesException()
        {
            var middleware = Create<ArgumentException>();

            await Should.ThrowAsync<InvalidOperationException>(() =>
                middleware.Execute(_ => Task.FromException(new InvalidOperationException()), CancellationToken.None));
        }

        [Fact]
        public async Task Execute_WhenDerivedExceptionTypeThrown_SwallowsException()
        {
            var middleware = Create<Exception>();

            await Should.NotThrowAsync(() =>
                middleware.Execute(_ => Task.FromException(new InvalidOperationException()), CancellationToken.None));
        }

        [Fact]
        public async Task Execute_WhenPredicateReturnsFalse_PropagatesException()
        {
            var middleware = Create<InvalidOperationException>(predicate: _ => false);

            await Should.ThrowAsync<InvalidOperationException>(() =>
                middleware.Execute(_ => Task.FromException(new InvalidOperationException()), CancellationToken.None));
        }

        [Fact]
        public async Task Execute_WhenPredicateReturnsTrue_SwallowsException()
        {
            var middleware = Create<InvalidOperationException>(predicate: _ => true);

            await Should.NotThrowAsync(() =>
                middleware.Execute(_ => Task.FromException(new InvalidOperationException()), CancellationToken.None));
        }

        [Fact]
        public async Task Execute_WhenMatchingExceptionTypeThrown_LogsError()
        {
            var logger = new FakeLogger<ErrorHandlingMiddleware<InvalidOperationException>>();
            var exception = new InvalidOperationException("test error");
            var middleware = new ErrorHandlingMiddleware<InvalidOperationException>(logger);

            await middleware.Execute(_ => Task.FromException(exception), CancellationToken.None);

            var log = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
            log.Level.ShouldBe(LogLevel.Error);
            log.Exception.ShouldBeSameAs(exception);
        }

        private static ErrorHandlingMiddleware<TException> Create<TException>(Func<TException, bool>? predicate = null)
            where TException : Exception =>
            new(new FakeLogger<ErrorHandlingMiddleware<TException>>(), predicate);
    }
}
