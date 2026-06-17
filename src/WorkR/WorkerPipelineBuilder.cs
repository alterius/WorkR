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
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(step);

            var mw = WithMiddleware(middleware);

            return new WorkerPipelineBuilder<TIn, TNext>(
                [.._workerNames, name],
                (sp, value, next, ct) =>
                    _pipeline(sp, value, (sp2, value2, ct2) =>
                        mw(sp2, (sp3, ct3) =>
                            step(sp3, value2, next, ct3), ct2),
                        ct));
        }

        internal WorkerPipelineBuilder<TIn> Finally(
            string name,
            WorkerPipelineStage<TOut> step,
            Action<WorkerMiddlewarePipelineBuilder>? middleware = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(step);

            var mw = WithMiddleware(middleware);

            return new WorkerPipelineBuilder<TIn>(
                [.._workerNames, name],
                (sp, value, ct) =>
                    _pipeline(sp, value, (sp2, value2, ct2) =>
                        mw(sp2, (sp3, ct3) =>
                            step(sp3, value2, ct3), ct2),
                        ct));
        }

        private static WorkerMiddlewarePipeline WithMiddleware(
            Action<WorkerMiddlewarePipelineBuilder>? middleware)
        {
            if (middleware is null)
            {
                return static (sp, step, ct) => step(sp, ct);
            }

            var builder = new WorkerMiddlewarePipelineBuilder();
            middleware(builder);
            return builder.Build();
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

        internal IWorkerPipeline<TIn> Build(IServiceProvider serviceProvider)
        {
            return WorkerPipeline.Create<TIn>((value, ct) => _pipeline(serviceProvider, value, ct));
        }
    }
}
