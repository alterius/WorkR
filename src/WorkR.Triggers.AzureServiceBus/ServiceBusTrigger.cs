using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace WorkR.Triggers.AzureServiceBus
{
    internal sealed class ServiceBusTriggerBase<TContext> : ITrigger<TContext>, IAsyncDisposable
        where TContext : TriggerContext
    {
        private readonly ServiceBusProcessor _serviceBusProcessor;
        private readonly Func<Guid, ProcessMessageEventArgs, Task<TContext>> _contextFactory;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger _logger;

        private ServiceBusTriggerBase(
            ServiceBusProcessor serviceBusProcessor,
            Func<Guid, ProcessMessageEventArgs, Task<TContext>> contextFactory,
            TimeProvider timeProvider,
            ILogger logger)
        {
            _serviceBusProcessor = serviceBusProcessor;
            _timeProvider = timeProvider;
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public ServiceBusTriggerBase(
            ServiceBusClient serviceBusClient,
            string queueName,
            Func<Guid, ProcessMessageEventArgs, Task<TContext>> contextFactory,
            TimeProvider timeProvider,
            ILogger logger,
            ServiceBusProcessorOptions? options = null)
                : this(serviceBusClient.CreateProcessor(queueName, options), contextFactory, timeProvider, logger)
        {
        }

        public ServiceBusTriggerBase(
            ServiceBusClient serviceBusClient,
            string topicName,
            string subscriptionName,
            Func<Guid, ProcessMessageEventArgs, Task<TContext>> contextFactory,
            TimeProvider timeProvider,
            ILogger logger,
            ServiceBusProcessorOptions? options = null)
                : this(serviceBusClient.CreateProcessor(topicName, subscriptionName, options), contextFactory, timeProvider, logger)
        {
        }

        public async Task ExecuteAsync(IWorkerPipeline<TContext> workerPipeline, CancellationToken stoppingToken)
        {
            _serviceBusProcessor.ProcessMessageAsync += async args =>
            {
                var executionId = Guid.NewGuid();

                using var _ = _logger.BeginScope(
                    new LogScope("MessageId", args.Message.MessageId));

                TContext context;

                try
                {
                    context = await _contextFactory(executionId, args).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                    when (args.CancellationToken.IsCancellationRequested)
                {
                    // Expected shutdown; rethrow so the message is abandoned and redelivered
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create trigger context from service bus message");

                    try
                    {
                        await args.DeadLetterMessageAsync(
                            args.Message,
                            deadLetterReason: "TriggerContextCreationFailed",
                            deadLetterErrorDescription: ex.Message,
                            cancellationToken: CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception ex2)
                    {
                        _logger.LogError(ex2, "Failed to dead letter message");
                    }

                    return;
                }

                await workerPipeline.ExecuteAsync(context, args.CancellationToken).ConfigureAwait(false);
            };

            _serviceBusProcessor.ProcessErrorAsync += args =>
            {
                if (args.ErrorSource == ServiceBusErrorSource.ProcessMessageCallback)
                {
                    if (args.Exception is OperationCanceledException && args.CancellationToken.IsCancellationRequested)
                    {
                        // Expected shutdown
                        return Task.CompletedTask;
                    }

                    // Worker pipeline failures are logged by WorkerService; avoid logging twice
                    _logger.LogDebug(args.Exception, "Service bus processor received error from message handler");
                    return Task.CompletedTask;
                }

                _logger.LogError(args.Exception, "Service bus processor failed with unhandled exception");

                return Task.CompletedTask;
            };

            using var _ = _logger.BeginScope(
                new LogScope("EntityPath", _serviceBusProcessor.EntityPath));

            _logger.LogInformation("Service bus trigger initialised");

            try
            {
                await _serviceBusProcessor.StartProcessingAsync(stoppingToken).ConfigureAwait(false);

                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, _timeProvider, stoppingToken).ConfigureAwait(false);
                }
                finally
                {
                    await _serviceBusProcessor.StopProcessingAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                _logger.LogInformation("Service bus trigger stopped");
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _serviceBusProcessor.DisposeAsync().ConfigureAwait(false);
        }
    }

    public sealed class ServiceBusTrigger : ITrigger<ServiceBusTriggerContext>, IAsyncDisposable
    {
        private readonly ServiceBusTriggerBase<ServiceBusTriggerContext> _inner;
        private readonly TimeProvider _timeProvider;

        public ServiceBusTrigger(
            ServiceBusClient serviceBusClient,
            string queueName,
            TimeProvider timeProvider,
            ILogger<ServiceBusTrigger> logger,
            ServiceBusProcessorOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(serviceBusClient);
            ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(logger);

            _inner = new ServiceBusTriggerBase<ServiceBusTriggerContext>(
                serviceBusClient,
                queueName,
                CreateContextAsync,
                timeProvider,
                logger,
                options);
            _timeProvider = timeProvider;
        }

        public ServiceBusTrigger(
            ServiceBusClient serviceBusClient,
            string topicName,
            string subscriptionName,
            TimeProvider timeProvider,
            ILogger<ServiceBusTrigger> logger,
            ServiceBusProcessorOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(serviceBusClient);
            ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
            ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(logger);

            _inner = new ServiceBusTriggerBase<ServiceBusTriggerContext>(
                serviceBusClient,
                topicName,
                subscriptionName,
                CreateContextAsync,
                timeProvider,
                logger,
                options);
            _timeProvider = timeProvider;
        }

        public Task ExecuteAsync(IWorkerPipeline<ServiceBusTriggerContext> workerPipeline, CancellationToken stoppingToken) =>
            _inner.ExecuteAsync(workerPipeline, stoppingToken);

        public async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync().ConfigureAwait(false);
        }

        private Task<ServiceBusTriggerContext> CreateContextAsync(Guid executionId, ProcessMessageEventArgs args) =>
            Task.FromResult(new ServiceBusTriggerContext(executionId, _timeProvider.GetUtcNow(), args));
    }

    public sealed class ServiceBusTrigger<T> : ITrigger<ServiceBusTriggerContext<T>>, IAsyncDisposable
    {
        private readonly ServiceBusTriggerBase<ServiceBusTriggerContext<T>> _inner;
        private readonly ServiceBusMessageDeserializer<T> _deserializer;
        private readonly TimeProvider _timeProvider;

        public ServiceBusTrigger(
            ServiceBusClient serviceBusClient,
            string queueName,
            ServiceBusMessageDeserializer<T> deserializer,
            TimeProvider timeProvider,
            ILogger<ServiceBusTrigger<T>> logger,
            ServiceBusProcessorOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(serviceBusClient);
            ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
            ArgumentNullException.ThrowIfNull(deserializer);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(logger);

            _inner = new ServiceBusTriggerBase<ServiceBusTriggerContext<T>>(
                serviceBusClient,
                queueName,
                CreateContextAsync,
                timeProvider,
                logger,
                options);
            _deserializer = deserializer;
            _timeProvider = timeProvider;
        }

        public ServiceBusTrigger(
            ServiceBusClient serviceBusClient,
            string topicName,
            string subscriptionName,
            ServiceBusMessageDeserializer<T> deserializer,
            TimeProvider timeProvider,
            ILogger<ServiceBusTrigger<T>> logger,
            ServiceBusProcessorOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(serviceBusClient);
            ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
            ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
            ArgumentNullException.ThrowIfNull(deserializer);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(logger);

            _inner = new ServiceBusTriggerBase<ServiceBusTriggerContext<T>>(
                serviceBusClient,
                topicName,
                subscriptionName,
                CreateContextAsync,
                timeProvider,
                logger,
                options);
            _deserializer = deserializer;
            _timeProvider = timeProvider;
        }

        public Task ExecuteAsync(IWorkerPipeline<ServiceBusTriggerContext<T>> workerPipeline, CancellationToken stoppingToken) =>
            _inner.ExecuteAsync(workerPipeline, stoppingToken);

        public async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync().ConfigureAwait(false);
        }

        private async Task<ServiceBusTriggerContext<T>> CreateContextAsync(Guid executionId, ProcessMessageEventArgs args) =>
            new(
                executionId,
                _timeProvider.GetUtcNow(),
                await _deserializer(args).ConfigureAwait(false),
                args);
    }
}
