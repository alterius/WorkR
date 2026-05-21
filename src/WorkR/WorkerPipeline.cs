using Microsoft.Extensions.DependencyInjection;
using WorkR.Middleware;

namespace WorkR
{
    internal delegate Task WorkerPipelineDelegate<TOut>(IServiceProvider sp, TOut value, CancellationToken cancellationToken);
    internal delegate Task WorkerPipelineDelegate<TIn, TOut>(IServiceProvider sp, TIn value, WorkerPipelineDelegate<TOut> next, CancellationToken cancellationToken);

    internal static class WorkerPipeline
    {
        internal static WorkerPipeline<TIn, TIn> Create<TIn>() =>
            new((sp, value, next, ct) => next(sp, value, ct));
    }

    internal class WorkerPipeline<TIn, TOut>
    {
        private readonly WorkerPipelineDelegate<TIn, TOut> _pipeline;

        internal WorkerPipeline(WorkerPipelineDelegate<TIn, TOut> pipeline)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            _pipeline = pipeline;
        }

        internal WorkerPipeline<TIn, TNext> Then<TWorker, TNext>(
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TOut, TNext>
        {
            var current = _pipeline;
            var applyMiddleware = BuildMiddleware(middleware);

            return new WorkerPipeline<TIn, TNext>(
                (sp, value, next, ct) => current(sp, value, (sp2, value2, ct2) =>
                    applyMiddleware((sp3, ct3) =>
                    {
                        var worker = sp3.GetRequiredService<TWorker>();
                        return worker.ExecuteAsync(value2, (v, ct4) => next(sp3, v, ct4), ct3);
                    })(sp2, ct2), ct));
        }

        internal WorkerPipeline<TIn> Finally<TWorker>(
            Action<MiddlewarePipelineBuilder>? middleware = null)
                where TWorker : IWorker<TOut>
        {
            var current = _pipeline;
            var applyMiddleware = BuildMiddleware(middleware);

            return new WorkerPipeline<TIn>(
                (sp, value, ct) => current(sp, value, (sp2, value2, ct2) =>
                    applyMiddleware((sp3, ct3) =>
                    {
                        var worker = sp3.GetRequiredService<TWorker>();
                        return worker.ExecuteAsync(value2, ct3);
                    })(sp2, ct2), ct));
        }

        private static Func<PipelineMiddlewareDelegate, PipelineMiddlewareDelegate> BuildMiddleware(
            Action<MiddlewarePipelineBuilder>? configure)
        {
            var builder = new MiddlewarePipelineBuilder();
            configure?.Invoke(builder);
            return builder.Build;
        }
    }

    public class WorkerPipeline<TIn>
    {
        private readonly WorkerPipelineDelegate<TIn> _pipeline;

        internal WorkerPipeline(WorkerPipelineDelegate<TIn> pipeline)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            _pipeline = pipeline;
        }

        internal WorkerDelegate<TIn> Build(IServiceProvider serviceProvider)
        {
            return (value, ct) => _pipeline(serviceProvider, value, ct);
        }
    }
}
