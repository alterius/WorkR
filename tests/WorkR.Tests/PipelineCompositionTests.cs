using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;
using WorkR.Triggers.RunOnce;

namespace WorkR.Tests
{
    [Trait("Category", "L1")]
    public class PipelineCompositionTests
    {
        [Fact]
        public async Task ThenFinally_TransformsValueBeforeTerminalWorker()
        {
            string? captured = null;
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddLogging(b => b.ClearProviders());
                    services.AddSingleton(new Capture(v =>
                    {
                        captured = v;
                        done.TrySetResult();
                    }));
                    services.AddRunOnceWorker(builder =>
                        builder
                            .AddWorker<StringProducerWorker, string>()
                            .AddWorker<CapturingWorker>());
                })
                .Build();

            await host.StartAsync(TestContext.Current.CancellationToken);
            await done.Task.WaitAsync(TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            captured.ShouldBe("hello");
        }

        [Fact]
        public async Task ThenThenFinally_AppliesTransformationsInOrder()
        {
            string? captured = null;
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddLogging(b => b.ClearProviders());
                    services.AddSingleton(new Capture(v =>
                    {
                        captured = v;
                        done.TrySetResult();
                    }));
                    services.AddRunOnceWorker(builder =>
                        builder
                            .AddWorker<StringProducerWorker, string>()   // "hello" → next
                            .AddWorker<UpperCaseWorker, string>()         // "HELLO" → next
                            .AddWorker<CapturingWorker>());               // captures "HELLO"
                })
                .Build();

            await host.StartAsync(TestContext.Current.CancellationToken);
            await done.Task.WaitAsync(TestContext.Current.CancellationToken);
            await host.StopAsync(TestContext.Current.CancellationToken);

            captured.ShouldBe("HELLO");
        }

        private sealed record Capture(Action<string> OnCapture);

        private sealed class StringProducerWorker : IWorker<EmptyTriggerContext, string>
        {
            public Task ExecuteAsync(EmptyTriggerContext source, IWorkerPipeline<string> next, CancellationToken cancellationToken) =>
                next.ExecuteAsync("hello", cancellationToken);
        }

        private sealed class UpperCaseWorker : IWorker<string, string>
        {
            public Task ExecuteAsync(string source, IWorkerPipeline<string> next, CancellationToken cancellationToken) =>
                next.ExecuteAsync(source.ToUpper(), cancellationToken);
        }

        private sealed class CapturingWorker(Capture capture) : IWorker<string>
        {
            public Task ExecuteAsync(string source, CancellationToken cancellationToken)
            {
                capture.OnCapture(source);
                return Task.CompletedTask;
            }
        }
    }
}
