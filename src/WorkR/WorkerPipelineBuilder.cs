using Microsoft.Extensions.DependencyInjection;
using WorkR.Middleware;

namespace WorkR
{
    /// <summary>
    /// A terminal pipeline stage: runs a worker against a value with no further step.
    /// </summary>
    internal delegate Task WorkerPipelineStage<in TIn>(IServiceProvider sp, TIn value, CancellationToken cancellationToken);

    /// <summary>
    /// A transforming pipeline stage: runs a worker and forwards a result via <paramref name="next"/>.
    /// </summary>
    internal delegate Task WorkerPipelineStage<in TIn, out TOut>(IServiceProvider sp, TIn value, WorkerPipelineStage<TOut> next, CancellationToken cancellationToken);

    /// <summary>
    /// Factory for the internal pipeline builder.
    /// </summary>
    internal static class WorkerPipelineBuilder
    {
        /// <summary>
        /// Creates an empty builder whose seed stage passes the value straight to the next stage.
        /// </summary>
        internal static WorkerPipelineBuilder<TIn, TIn> Create<TIn>() =>
            new([], static (sp, value, next, ct) => next(sp, value, ct));
    }

    /// <summary>
    /// Builds a worker pipeline by composing stages, tracking <typeparamref name="TIn"/> (the
    /// pipeline's input) and <typeparamref name="TOut"/> (the type the next stage receives).
    /// </summary>
    /// <typeparam name="TIn">The pipeline's input type.</typeparam>
    /// <typeparam name="TOut">The value type the next stage will receive.</typeparam>
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

        /// <summary>
        /// Appends a transforming stage, wrapped in its middleware, and returns a builder
        /// expecting the next value type.
        /// </summary>
        /// <remarks>
        /// The new stage is nested inside the existing pipeline so stages execute in registration
        /// order, each running within its own middleware.
        /// </remarks>
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

        /// <summary>
        /// Appends a terminal stage, wrapped in its middleware, closing the pipeline.
        /// </summary>
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

        /// <summary>
        /// Builds the middleware pipeline for a stage, or a pass-through when none is configured.
        /// </summary>
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

    /// <summary>
    /// A fully composed worker pipeline that ends in a terminal worker. Produced by the
    /// registration builder and consumed internally; not intended to be used directly.
    /// </summary>
    /// <typeparam name="TIn">The input type the pipeline accepts, i.e. the trigger's context type.</typeparam>
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

        /// <summary>
        /// Materialises the composed stages into a runnable pipeline named by joining the worker
        /// names with <c>" -&gt; "</c>. Each execution runs within its own dependency injection scope.
        /// </summary>
        internal INamedWorkerPipeline<TIn> Build(IServiceProvider serviceProvider)
        {
            ArgumentNullException.ThrowIfNull(serviceProvider);

            return new DelegateWorkerPipeline<TIn>(
                string.Join(" -> ", _workerNames),
                async (value, ct) =>
                {
                    await using var scope = serviceProvider.CreateAsyncScope();
                    await _pipeline(scope.ServiceProvider, value, ct);
                });
        }
    }
}
