using Microsoft.Extensions.Time.Testing;
using Shouldly;
using WorkR.Middleware;

namespace WorkR.Tests.Middleware
{
    [Trait("Category", "L0")]
    public class TimeoutMiddlewareTests
    {
        [Fact]
        public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
        {
            Should.Throw<ArgumentNullException>(() =>
                new TimeoutMiddleware(null!, TimeSpan.FromSeconds(1)));
        }

        [Fact]
        public void Constructor_WhenTimeoutIsZero_ThrowsArgumentOutOfRangeException()
        {
            Should.Throw<ArgumentOutOfRangeException>(() =>
                new TimeoutMiddleware(TimeProvider.System, TimeSpan.Zero));
        }

        [Fact]
        public void Constructor_WhenTimeoutIsNegative_ThrowsArgumentOutOfRangeException()
        {
            Should.Throw<ArgumentOutOfRangeException>(() =>
                new TimeoutMiddleware(TimeProvider.System, TimeSpan.FromSeconds(-1)));
        }

        [Fact]
        public async Task Execute_WhenNextCompletesBeforeTimeout_Succeeds()
        {
            var middleware = new TimeoutMiddleware(TimeProvider.System, TimeSpan.FromSeconds(30));

            await Should.NotThrowAsync(() =>
                middleware.Execute(_ => Task.CompletedTask, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Execute_WhenTimeoutElapses_ThrowsOperationCanceledException()
        {
            var timeProvider = new FakeTimeProvider();
            var middleware = new TimeoutMiddleware(timeProvider, TimeSpan.FromSeconds(5));
            var nextStarted = new SemaphoreSlim(0, 1);

            var task = middleware.Execute(async ct =>
            {
                nextStarted.Release();
                await Task.Delay(Timeout.Infinite, ct);
            }, TestContext.Current.CancellationToken);

            await nextStarted.WaitAsync(TestContext.Current.CancellationToken);
            timeProvider.Advance(TimeSpan.FromSeconds(5));

            await Should.ThrowAsync<OperationCanceledException>(() => task);
        }

        [Fact]
        public async Task Execute_WhenCallerCancellationTokenCancelled_ThrowsOperationCanceledException()
        {
            var middleware = new TimeoutMiddleware(TimeProvider.System, TimeSpan.FromSeconds(30));
            using var cts = new CancellationTokenSource();
            var nextStarted = new SemaphoreSlim(0, 1);

            var task = middleware.Execute(async ct =>
            {
                nextStarted.Release();
                await Task.Delay(Timeout.Infinite, ct);
            }, cts.Token);

            await nextStarted.WaitAsync(TestContext.Current.CancellationToken);
            await cts.CancelAsync();

            await Should.ThrowAsync<OperationCanceledException>(() => task);
        }
    }
}
