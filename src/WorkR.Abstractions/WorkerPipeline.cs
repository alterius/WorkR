namespace WorkR
{
    public static class WorkerPipeline
    {
        public static IWorkerPipeline<TIn> Create<TIn>(Func<TIn, CancellationToken, Task> execute) =>
            new DelegateWorkerPipeline<TIn>(execute);

        private sealed class DelegateWorkerPipeline<TIn> : IWorkerPipeline<TIn>
        {
            private readonly Func<TIn, CancellationToken, Task> _execute;

            internal DelegateWorkerPipeline(Func<TIn, CancellationToken, Task> execute)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            }

            public Task ExecuteAsync(TIn value, CancellationToken cancellationToken) =>
                _execute(value, cancellationToken);
        }
    }
}
