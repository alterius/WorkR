using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging;

namespace WorkR.Triggers.AzureStorageQueues
{
    internal class StorageQueueTriggerBase<TContext> : ITrigger<TContext>
        where TContext : TriggerContext
    {
        private readonly QueueClient _queueClient;
        private readonly StorageQueueTriggerConfig _config;
        private readonly Func<QueueMessage, Task<TContext>> _contextFactory;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger _logger;

        public StorageQueueTriggerBase(
            QueueClient queueClient,
            StorageQueueTriggerConfig config,
            Func<QueueMessage, Task<TContext>> contextFactory,
            TimeProvider timeProvider,
            ILogger logger)
        {
            ArgumentNullException.ThrowIfNull(queueClient);
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(contextFactory);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(logger);

            _queueClient = queueClient;
            _config = config;
            _contextFactory = contextFactory;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Execute(WorkerDelegate<TContext> next, CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Response<QueueMessage[]> response;

                try
                {
                    response = await _queueClient.ReceiveMessagesAsync(
                        maxMessages: _config.MaxMessages,
                        visibilityTimeout: _config.VisibilityTimeout,
                        cancellationToken: stoppingToken);
                }
                catch (RequestFailedException ex)
                    when (ex.Status is 429 or 500 or 503)
                {
                    await Task.Delay(_config.PollingInterval, _timeProvider, stoppingToken);
                    continue;
                }
                catch (HttpRequestException)
                {
                    await Task.Delay(_config.PollingInterval, _timeProvider, stoppingToken);
                    continue;
                }

                var messages = response.Value;

                if (messages.Length == 0)
                {
                    await Task.Delay(_config.PollingInterval, _timeProvider, stoppingToken);
                    continue;
                }

                foreach (var message in messages)
                {
                    TContext context;

                    try
                    {
                        context = await _contextFactory(message);
                    }
                    catch (Exception ex)
                    {
                        // Todo: What to do with poison messages to prevent them from going around and around?
                        _logger.LogError(ex, "Failed to create trigger context from queue message");
                        continue;
                    }
                    
                    await next(context, stoppingToken);
                }
            }
        }
    }

    public sealed class StorageQueueTrigger : ITrigger<StorageQueueTriggerContext>
    {
        private readonly StorageQueueTriggerBase<StorageQueueTriggerContext> _inner;
        private readonly QueueClient _queueClient;
        private readonly TimeProvider _timeProvider;

        public StorageQueueTrigger(
            QueueClient queueClient,
            StorageQueueTriggerConfig config,
            TimeProvider timeProvider,
            ILogger<StorageQueueTrigger> logger)
        {
            ArgumentNullException.ThrowIfNull(queueClient);
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(logger);

            _inner = new StorageQueueTriggerBase<StorageQueueTriggerContext>(queueClient, config, CreateContext, timeProvider, logger);
            _queueClient = queueClient;
            _timeProvider = timeProvider;
        }

        public Task Execute(WorkerDelegate<StorageQueueTriggerContext> next, CancellationToken stoppingToken) =>
            _inner.Execute(next, stoppingToken);

        private Task<StorageQueueTriggerContext> CreateContext(QueueMessage queueMessage) =>
            Task.FromResult(
                new StorageQueueTriggerContext(
                    _timeProvider.GetUtcNow(),
                    queueMessage,
                    ct => _queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt, ct)));
    }

    public sealed class StorageQueueTrigger<T> : ITrigger<StorageQueueTriggerContext<T>>
    {
        private readonly StorageQueueTriggerBase<StorageQueueTriggerContext<T>> _inner;
        private readonly QueueClient _queueClient;
        private readonly IStorageQueueMessageDeserializer<T> _deserializer;
        private readonly TimeProvider _timeProvider;

        public StorageQueueTrigger(
            QueueClient queueClient,
            StorageQueueTriggerConfig config,
            IStorageQueueMessageDeserializer<T> deserializer,
            TimeProvider timeProvider,
            ILogger<StorageQueueTrigger<T>> logger)
        {
            ArgumentNullException.ThrowIfNull(queueClient);
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(deserializer);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(logger);

            _inner = new StorageQueueTriggerBase<StorageQueueTriggerContext<T>>(queueClient, config, CreateContext, timeProvider, logger);
            _queueClient = queueClient;
            _deserializer = deserializer;
            _timeProvider = timeProvider;
        }

        public Task Execute(WorkerDelegate<StorageQueueTriggerContext<T>> next, CancellationToken stoppingToken) =>
            _inner.Execute(next, stoppingToken);

        private async Task<StorageQueueTriggerContext<T>> CreateContext(QueueMessage queueMessage) =>
            new(
                _timeProvider.GetUtcNow(),
                await _deserializer.Deserialize(queueMessage),
                queueMessage,
                ct => _queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt, ct));
    }
}
