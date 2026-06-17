namespace WorkR.Tests
{
    /// <summary>A no-op terminal worker for <see cref="EmptyTriggerContext"/>.</summary>
    internal sealed class FakeWorker : IWorker<EmptyTriggerContext>
    {
        public Task ExecuteAsync(EmptyTriggerContext source, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
