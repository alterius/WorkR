using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace WorkR.Triggers.Timers.Tests
{
    [Trait("Category", "L1")]
    public class TimerWorkerTests
    {
        [Fact]
        public async Task DelayWorker_EachInvocationRunsInNewScope()
        {
            var log = new ScopeLog();

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddLogging(b => b.ClearProviders());
                    services.AddSingleton(log);
                    services.AddScoped<ScopedId>();
                    services.AddDelayWorker<ScopeCapturingWorker>(TimeSpan.FromMilliseconds(1));
                })
                .Build();

            await host.StartAsync(TestContext.Current.CancellationToken);
            await log.TwoInvocations.WaitAsync(TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            log.CapturedIds.ShouldBeUnique();
            log.CapturedIds.Count.ShouldBeGreaterThanOrEqualTo(2);
        }

        [Fact]
        public async Task ScheduledWorker_WithRunOnStartup_FiresImmediately()
        {
            var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddLogging(b => b.ClearProviders());
                    services.AddSingleton(new CallSignal(() => fired.TrySetResult()));
                    services.AddScheduledWorker<SignalWorker>("* * * * *", runOnStartup: true);
                })
                .Build();

            await host.StartAsync(TestContext.Current.CancellationToken);
            await fired.Task.WaitAsync(TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);
        }

        private sealed class ScopedId
        {
            public Guid Value { get; } = Guid.NewGuid();
        }

        private sealed class ScopeLog
        {
            private readonly List<Guid> _ids = [];
            private readonly TaskCompletionSource _twoInvocations = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task TwoInvocations => _twoInvocations.Task;
            public IReadOnlyList<Guid> CapturedIds { get { lock (_ids) return [.. _ids]; } }

            public void Add(Guid id)
            {
                lock (_ids)
                {
                    _ids.Add(id);
                    if (_ids.Count >= 2) _twoInvocations.TrySetResult();
                }
            }
        }

        private sealed class ScopeCapturingWorker(ScopedId scopedId, ScopeLog log) : IWorker<EmptyTriggerContext>
        {
            public Task Execute(EmptyTriggerContext context, CancellationToken ct)
            {
                log.Add(scopedId.Value);
                return Task.CompletedTask;
            }
        }

        private sealed record CallSignal(Action OnSignal);

        private sealed class SignalWorker(CallSignal signal) : IWorker<EmptyTriggerContext>
        {
            public Task Execute(EmptyTriggerContext context, CancellationToken ct)
            {
                signal.OnSignal();
                return Task.CompletedTask;
            }
        }
    }
}
