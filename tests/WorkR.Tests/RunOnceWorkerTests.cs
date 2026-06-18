using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;
using WorkR.Triggers.RunOnce;

namespace WorkR.Tests
{
    [Trait("Category", "L1")]
    public class RunOnceWorkerTests
    {
        [Fact]
        public async Task RunOnce_ExecutesWorkerOnce()
        {
            var callCount = 0;
            var executed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddLogging(b => b.ClearProviders());
                    services.AddSingleton(new WorkerSignal(() =>
                    {
                        Interlocked.Increment(ref callCount);
                        executed.TrySetResult();
                    }));
                    services.AddRunOnceWorker(builder => builder.AddWorker<SignalWorker>());
                })
                .Build();

            await host.StartAsync(TestContext.Current.CancellationToken);
            await executed.Task.WaitAsync(TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            callCount.ShouldBe(1);
        }

        [Fact]
        public async Task RunOnce_WhenWorkerThrows_ErrorSwallowedByTrigger()
        {
            var workerRan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddLogging(b => b.ClearProviders());
                    services.AddSingleton(workerRan);
                    services.AddRunOnceWorker(builder => builder.AddWorker<ThrowingWorker>());
                })
                .Build();

            await host.StartAsync(TestContext.Current.CancellationToken);
            await workerRan.Task.WaitAsync(TestContext.Current.CancellationToken);

            var workerService = host.Services.GetServices<IHostedService>()
                .OfType<BackgroundService>()
                .Single();

            await workerService.ExecuteTask!.WaitAsync(TestContext.Current.CancellationToken);

            workerService.ExecuteTask!.IsCompletedSuccessfully.ShouldBeTrue();
            await host.StopAsync(TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task RunOnce_WorkerReceivesDependenciesFromDI()
        {
            var executed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var dependency = new ExplicitDependency();

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddLogging(b => b.ClearProviders());
                    services.AddSingleton(dependency);
                    services.AddSingleton(executed);
                    services.AddRunOnceWorker(builder => builder.AddWorker<DependencyCapturingWorker>());
                })
                .Build();

            await host.StartAsync(TestContext.Current.CancellationToken);
            await executed.Task.WaitAsync(TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            dependency.WasCaptured.ShouldBeTrue();
        }

        private sealed class WorkerSignal(Action onSignal)
        {
            public void Signal() => onSignal();
        }

        private sealed class SignalWorker(WorkerSignal signal) : IWorker<EmptyTriggerContext>
        {
            public Task ExecuteAsync(EmptyTriggerContext context, CancellationToken cancellationToken)
            {
                signal.Signal();
                return Task.CompletedTask;
            }
        }

        private sealed class ThrowingWorker(TaskCompletionSource signal) : IWorker<EmptyTriggerContext>
        {
            public Task ExecuteAsync(EmptyTriggerContext context, CancellationToken cancellationToken)
            {
                signal.TrySetResult();
                throw new InvalidOperationException("test error");
            }
        }

        private sealed class ExplicitDependency
        {
            public bool WasCaptured { get; private set; }
            public void Capture() => WasCaptured = true;
        }

        private sealed class DependencyCapturingWorker(
            ExplicitDependency dependency,
            TaskCompletionSource signal) : IWorker<EmptyTriggerContext>
        {
            public Task ExecuteAsync(EmptyTriggerContext context, CancellationToken cancellationToken)
            {
                dependency.Capture();
                signal.TrySetResult();
                return Task.CompletedTask;
            }
        }
    }
}
