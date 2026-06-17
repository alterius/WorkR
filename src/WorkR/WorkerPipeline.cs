using WorkR.Middleware;

namespace WorkR
{
    internal delegate Task WorkerPipelineDelegate<TOut>(IServiceProvider sp, TOut value, CancellationToken cancellationToken);
    internal delegate Task WorkerPipelineDelegate<TIn, TOut>(IServiceProvider sp, TIn value, WorkerPipelineDelegate<TOut> next, CancellationToken cancellationToken);

    // A single pipeline step, as seen by the executor: run something for the current value,
    // optionally producing the next value. The registration layer decides how the step is
    // obtained (resolved from DI, built by a factory, an inline delegate); the pipeline only runs it.
    internal delegate Task WorkerStep<in TIn, TNext>(IServiceProvider sp, TIn value, WorkerPipelineDelegate<TNext> next, CancellationToken cancellationToken);
    internal delegate Task WorkerStep<in TIn>(IServiceProvider sp, TIn value, CancellationToken cancellationToken);

    internal static class WorkerPipeline
    {
        internal static WorkerPipeline<TIn, TIn> Create<TIn>() =>
            new((sp, value, next, ct) => next(sp, value, ct), []);
    }

    internal sealed class WorkerPipeline<TIn, TOut>
    {
        private readonly WorkerPipelineDelegate<TIn, TOut> _pipeline;
        private readonly IReadOnlyList<string> _workerNames;

        internal WorkerPipeline(WorkerPipelineDelegate<TIn, TOut> pipeline, IReadOnlyList<string> workerNames)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(workerNames);

            _pipeline = pipeline;
            _workerNames = workerNames;
        }

        internal WorkerPipeline<TIn, TNext> Then<TNext>(
            string name,
            WorkerStep<TOut, TNext> step,
            Action<MiddlewarePipelineBuilder>? middleware = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(step);

            var current = _pipeline;
            var applyMiddleware = BuildMiddleware(middleware);

            return new WorkerPipeline<TIn, TNext>(
                (sp, value, next, ct) => current(sp, value, (sp2, value2, ct2) =>
                    applyMiddleware((sp3, ct3) =>
                        step(sp3, value2, next, ct3))(sp2, ct2), ct),
                [.._workerNames, name]);
        }

        internal WorkerPipeline<TIn> Finally(
            string name,
            WorkerStep<TOut> step,
            Action<MiddlewarePipelineBuilder>? middleware = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(step);

            var current = _pipeline;
            var applyMiddleware = BuildMiddleware(middleware);

            return new WorkerPipeline<TIn>(
                (sp, value, ct) => current(sp, value, (sp2, value2, ct2) =>
                    applyMiddleware((sp3, ct3) =>
                        step(sp3, value2, ct3))(sp2, ct2), ct),
                [.._workerNames, name]);
        }

        private static Func<PipelineMiddlewareDelegate, PipelineMiddlewareDelegate> BuildMiddleware(
            Action<MiddlewarePipelineBuilder>? configure)
        {
            var builder = new MiddlewarePipelineBuilder();
            configure?.Invoke(builder);
            return builder.Build;
        }
    }

    public sealed class WorkerPipeline<TIn>
    {
        private readonly WorkerPipelineDelegate<TIn> _pipeline;
        private readonly IReadOnlyList<string> _workerNames;

        internal WorkerPipeline(WorkerPipelineDelegate<TIn> pipeline, IReadOnlyList<string> workerNames)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(workerNames);

            if (workerNames.Count <= 0)
            {
                throw new ArgumentException($"{nameof(workerNames)} must contain at least one item.", nameof(workerNames));
            }

            _pipeline = pipeline;
            _workerNames = workerNames;
        }

        internal IReadOnlyList<string> WorkerNames => _workerNames;

        internal WorkerDelegate<TIn> Build(IServiceProvider serviceProvider)
        {
            return (value, ct) => _pipeline(serviceProvider, value, ct);
        }
    }
}
