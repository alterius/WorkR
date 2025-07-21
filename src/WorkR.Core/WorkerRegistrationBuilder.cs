namespace WorkR
{
    public class WorkerRegistrationBuilder
    {
        private readonly List<Action<WorkerPipelineBuilder>> _steps = [];

        public WorkerRegistrationBuilder AddDelay(TimeSpan delay)
        {
            _steps.Add(b => b.WithDelay(delay));
            return this;
        }

        public WorkerRegistrationBuilder AddErrorHandling(Func<ExecutionContext, Exception, Task>? onException = null)
        {
            _steps.Add(b => b.WithExceptionHandling(onException));
            return this;
        }

        public WorkerPipelineBuilder Build(IServiceProvider serviceProvider)
        {
            var pipelineBuilder = new WorkerPipelineBuilder(serviceProvider);

            foreach (var step in _steps)
            {
                step(pipelineBuilder);
            }

            return pipelineBuilder;
        }
    }
}
