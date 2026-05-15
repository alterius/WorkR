using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Shouldly;
using WorkR.Middleware;

namespace WorkR.Tests.Middleware
{
    public class FireAndForgetMiddlewareTests
    {
        [Fact]
        public void Execute_ReturnsImmediatelyWithoutAwaitingNext()
        {
            var middleware = new FireAndForgetMiddleware(NullLogger<FireAndForgetMiddleware>.Instance);
            var neverCompletes = new TaskCompletionSource();

            var task = middleware.Execute(_ => neverCompletes.Task, TestContext.Current.CancellationToken);

            task.IsCompleted.ShouldBeTrue();
            neverCompletes.SetCanceled(TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task Execute_WhenNextThrowsNonCancellationException_DoesNotPropagateException()
        {
            var middleware = new FireAndForgetMiddleware(NullLogger<FireAndForgetMiddleware>.Instance);

            await Should.NotThrowAsync(() =>
                middleware.Execute(_ => Task.FromException(new InvalidOperationException()), TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Execute_WhenNextThrowsNonCancellationException_LogsError()
        {
            var logger = new SignalOnLogLogger();
            var middleware = new FireAndForgetMiddleware(logger);
            var exception = new InvalidOperationException("test error");

            await middleware.Execute(_ => Task.FromException(exception), TestContext.Current.CancellationToken);
            await logger.WhenLogged.WaitAsync(TestContext.Current.CancellationToken);

            var log = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
            log.Level.ShouldBe(LogLevel.Error);
            log.Exception.ShouldBeSameAs(exception);
        }

        [Fact]
        public async Task Execute_WhenNextThrowsOperationCanceledException_DuringShutdown_DoesNotLog()
        {
            using var cts = new CancellationTokenSource();
            var logger = new FakeLogger<FireAndForgetMiddleware>();
            var middleware = new FireAndForgetMiddleware(logger);
            var nextDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            await middleware.Execute(_ =>
            {
                cts.Cancel();
                nextDone.TrySetResult();
                return Task.FromException(new OperationCanceledException());
            }, cts.Token);

            await nextDone.Task.WaitAsync(TestContext.Current.CancellationToken);

            logger.Collector.GetSnapshot().ShouldBeEmpty();
        }

        [Fact]
        public async Task Execute_WhenNextThrowsOperationCanceledException_WithoutShutdown_LogsError()
        {
            var logger = new SignalOnLogLogger();
            var middleware = new FireAndForgetMiddleware(logger);
            var exception = new OperationCanceledException();

            await middleware.Execute(_ => Task.FromException(exception), TestContext.Current.CancellationToken);
            await logger.WhenLogged.WaitAsync(TestContext.Current.CancellationToken);

            var log = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
            log.Level.ShouldBe(LogLevel.Error);
            log.Exception.ShouldBeSameAs(exception);
        }

        private sealed class SignalOnLogLogger : ILogger<FireAndForgetMiddleware>
        {
            private readonly FakeLogger<FireAndForgetMiddleware> _inner = new();
            private readonly TaskCompletionSource _logged = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task WhenLogged => _logged.Task;
            public FakeLogCollector Collector => _inner.Collector;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                _inner.Log(logLevel, eventId, state, exception, formatter);
                _logged.TrySetResult();
            }

            public bool IsEnabled(LogLevel logLevel) => _inner.IsEnabled(logLevel);
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _inner.BeginScope(state);
        }
    }
}
