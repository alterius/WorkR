using Microsoft.Extensions.DependencyInjection;

namespace WorkR
{
    public class WorkerPipelineBuilder
    {
        private readonly List<Func<IServiceProvider, IWorkerPipeline, IWorkerPipeline>> _steps = [];
        private readonly IServiceProvider _serviceProvider;

        public WorkerPipelineBuilder(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public WorkerPipelineBuilder With(Func<IServiceProvider, IWorkerPipeline, IWorkerPipeline> step)
        {
            _steps.Add(step);
            return this;
        }

        public WorkerPipelineBuilder WithDelay(TimeSpan delay)
        {
            return With((sp, inner) =>
                new DelayPipeline(inner, sp.GetRequiredService<TimeProvider>(), delay));
        }

        public WorkerPipelineBuilder WithExceptionHandling(Func<ExecutionContext, Exception, Task>? onExceptionThrown = null)
        {
            return With((_, inner) =>
                new ExceptionHandlingPipeline(inner, onExceptionThrown));
        }

        public IWorkerPipeline Build(Func<ExecutionContext, CancellationToken, Task> execute)
        {
            ArgumentNullException.ThrowIfNull(execute);

            IWorkerPipeline pipeline = new ExecutingPipeline(execute);

            foreach (var step in Enumerable.Reverse(_steps))
            {
                pipeline = step(_serviceProvider, pipeline);
            }

            return pipeline;
        }
    }
}
