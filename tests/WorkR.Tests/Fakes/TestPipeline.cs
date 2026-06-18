namespace WorkR.Tests
{
    /// <summary>
    /// Builds real <see cref="INamedWorkerPipeline{TIn}"/> instances for <see cref="EmptyTriggerContext"/>,
    /// so tests exercise the production pipeline/naming rather than a bespoke fake.
    /// </summary>
    internal static class TestPipeline
    {
        internal static INamedWorkerPipeline<EmptyTriggerContext> Named(
            string name = "FakeWorker",
            Func<EmptyTriggerContext, CancellationToken, Task>? run = null) =>
            Named([name], run);

        internal static INamedWorkerPipeline<EmptyTriggerContext> Named(
            string[] names,
            Func<EmptyTriggerContext, CancellationToken, Task>? run = null) =>
            new WorkerPipelineBuilder<EmptyTriggerContext>(
                    names,
                    (_, value, ct) => (run ?? ((_, _) => Task.CompletedTask))(value, ct))
                .Build(EmptyServiceProvider.Instance);
    }
}
