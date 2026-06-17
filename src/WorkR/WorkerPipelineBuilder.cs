using WorkR.Middleware;

namespace WorkR
{
    internal delegate Task WorkerPipelineStage<in TIn>(IServiceProvider sp, TIn value, CancellationToken cancellationToken);
    internal delegate Task WorkerPipelineStage<in TIn, out TOut>(IServiceProvider sp, TIn value, WorkerPipelineStage<TOut> next, CancellationToken cancellationToken);

    internal static class WorkerPipelineBuilder
    {
        internal static WorkerPipelineBuilder<TIn, TIn> Create<TIn>() =>
            new([], static (sp, value, next, ct) => next(sp, value, ct));
    }

    internal sealed class WorkerPipelineBuilder<TIn, TOut>
    {
        private readonly IReadOnlyList<string> _workerNames;
        private readonly WorkerPipelineStage<TIn, TOut> _pipeline;

        internal WorkerPipelineBuilder(IReadOnlyList<string> workerNames, WorkerPipelineStage<TIn, TOut> pipeline)
        {
            ArgumentNullException.ThrowIfNull(workerNames);
            ArgumentNullException.ThrowIfNull(pipeline);

            _pipeline = pipeline;
            _workerNames = workerNames;
        }

        internal WorkerPipelineBuilder<TIn, TNext> Then<TNext>(
            string name,
            WorkerPipelineStage<TOut, TNext> step,
            Action<MiddlewarePipelineBuilder>? middleware = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(step);

            var current = _pipeline;
            var applyMiddleware = BuildMiddleware(middleware);

            return new WorkerPipelineBuilder<TIn, TNext>(
                [.._workerNames, name],
                (sp, value, next, ct) =>
                    current(sp, value, (sp2, value2, ct2) =>
                        applyMiddleware((sp3, ct3) =>
                            step(sp3, value2, next, ct3))(sp2, ct2), ct));
        }

        internal WorkerPipelineBuilder<TIn> Finally(
            string name,
            WorkerPipelineStage<TOut> step,
            Action<MiddlewarePipelineBuilder>? middleware = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(step);

            var current = _pipeline;
            var applyMiddleware = BuildMiddleware(middleware);

            return new WorkerPipelineBuilder<TIn>(
                [.._workerNames, name],
                (sp, value, ct) =>
                    current(sp, value, (sp2, value2, ct2) =>
                        applyMiddleware((sp3, ct3) =>
                            step(sp3, value2, ct3))(sp2, ct2), ct));
        }

        private static Func<PipelineMiddlewareDelegate, PipelineMiddlewareDelegate> BuildMiddleware(
            Action<MiddlewarePipelineBuilder>? configure)
        {
            var builder = new MiddlewarePipelineBuilder();
            configure?.Invoke(builder);
            return builder.Build;
        }
    }

    public sealed class WorkerPipelineBuilder<TIn>
    {
        private readonly IReadOnlyList<string> _workerNames;
        private readonly WorkerPipelineStage<TIn> _pipeline;

        internal WorkerPipelineBuilder(IReadOnlyList<string> workerNames, WorkerPipelineStage<TIn> pipeline)
        {
            ArgumentNullException.ThrowIfNull(workerNames);
            ArgumentNullException.ThrowIfNull(pipeline);

            if (workerNames.Count <= 0)
            {
                throw new ArgumentException($"{nameof(workerNames)} must contain at least one item.", nameof(workerNames));
            }

            _pipeline = pipeline;
            _workerNames = workerNames;
        }

        internal IReadOnlyList<string> WorkerNames => _workerNames;

        internal WorkerPipeline<TIn> Build(IServiceProvider serviceProvider)
        {
            return (value, ct) => _pipeline(serviceProvider, value, ct);
        }
    }
}
