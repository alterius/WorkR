namespace WorkR
{
    /// <summary>
    /// An <see cref="INamedWorkerPipeline{TIn}"/> backed by a delegate.
    /// </summary>
    internal sealed class DelegateWorkerPipeline<TIn> : INamedWorkerPipeline<TIn>
    {
        private readonly string _name;
        private readonly Func<TIn, CancellationToken, Task> _pipeline;

        internal DelegateWorkerPipeline(string name, Func<TIn, CancellationToken, Task> pipeline)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(pipeline);

            _name = name;
            _pipeline = pipeline;
        }

        public string Name => _name;

        public Task ExecuteAsync(TIn value, CancellationToken cancellationToken) =>
            _pipeline(value, cancellationToken);
    }
}
