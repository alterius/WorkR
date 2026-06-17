namespace WorkR
{
    internal sealed class DelegateWorkerPipeline<TIn> : IWorkerPipeline<TIn>
    {
        private readonly Func<TIn, CancellationToken, Task> _pipeline;

        internal DelegateWorkerPipeline(Func<TIn, CancellationToken, Task> pipeline)
        {
            ArgumentNullException.ThrowIfNull(pipeline);

            _pipeline = pipeline;
        }

        public Task ExecuteAsync(TIn value, CancellationToken cancellationToken) =>
            _pipeline(value, cancellationToken);
    }
}
