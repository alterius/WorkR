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

        public async Task Execute(WorkerDelegate<TContext> next, CancellationToken stoppingToken)
        {
            _serviceBusProcessor.ProcessMessageAsync += async args =>
            {
                var executionId = Guid.NewGuid();

                using var _ = _logger.BeginScope(new
                {
                    ExecutionId = executionId,
                    args.Message.MessageId,
                });

                _logger.LogDebug("Service bus trigger executing...");

                var context = await _contextFactory(executionId, args).ConfigureAwait(false);
                await next(context, args.CancellationToken).ConfigureAwait(false);

                _logger.LogDebug("Service bus trigger executed");
            };

            _serviceBusProcessor.ProcessErrorAsync += args =>
            {
                _logger.LogError(args.Exception, "Service bus processor failed with unhandled exception");
                return Task.CompletedTask;
            };

            using var _ = _logger.BeginScope(new { _serviceBusProcessor.EntityPath });

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
                CreateContext,
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
                CreateContext,
                timeProvider,
                logger,
                options);
            _timeProvider = timeProvider;
        }

        public Task Execute(WorkerDelegate<ServiceBusTriggerContext> next, CancellationToken stoppingToken) =>
            _inner.Execute(next, stoppingToken);

        public async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync().ConfigureAwait(false);
        }

        private Task<ServiceBusTriggerContext> CreateContext(Guid executionId, ProcessMessageEventArgs args) =>
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
                CreateContext,
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
                CreateContext,
                timeProvider,
                logger,
                options);
            _deserializer = deserializer;
            _timeProvider = timeProvider;
        }

        public Task Execute(WorkerDelegate<ServiceBusTriggerContext<T>> next, CancellationToken stoppingToken) =>
            _inner.Execute(next, stoppingToken);

        public async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync().ConfigureAwait(false);
        }

        private async Task<ServiceBusTriggerContext<T>> CreateContext(Guid executionId, ProcessMessageEventArgs args) =>
            new(
                executionId,
                _timeProvider.GetUtcNow(),
                await _deserializer(args).ConfigureAwait(false),
                args);
    }
}
