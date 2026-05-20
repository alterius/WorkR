using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging;

namespace WorkR.Triggers.AzureStorageQueues
{
    internal sealed class StorageQueueTriggerBase<TContext> : ITrigger<TContext>
        where TContext : TriggerContext
    {
        private readonly QueueClient _queueClient;
        private readonly StorageQueueTriggerOptions _options;
        private readonly Func<Guid, QueueMessage, Task<TContext>> _contextFactory;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger _logger;

        public StorageQueueTriggerBase(
            QueueClient queueClient,
            StorageQueueTriggerOptions options,
            Func<Guid, QueueMessage, Task<TContext>> contextFactory,
            TimeProvider timeProvider,
            ILogger logger)
        {
            _queueClient = queueClient;
            _options = options;
            _contextFactory = contextFactory;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task Execute(WorkerDelegate<TContext> next, CancellationToken stoppingToken)
        {
            using var _ = _logger.BeginScope(new { QueueName = _queueClient.Name });

            _logger.LogInformation("Storage queue trigger initialised");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    Response<QueueMessage[]> response;

                    try
                    {
                        response = await _queueClient.ReceiveMessagesAsync(
                            maxMessages: _options.MaxMessages,
                            visibilityTimeout: _options.VisibilityTimeout,
                            cancellationToken: stoppingToken).ConfigureAwait(false);
                    }
                    catch (RequestFailedException ex)
                        when (ex.Status is 429 or 500 or 503)
                    {
                        await Task.Delay(_options.PollingInterval, _timeProvider, stoppingToken).ConfigureAwait(false);
                        continue;
                    }
                    catch (HttpRequestException)
                    {
                        await Task.Delay(_options.PollingInterval * 2, _timeProvider, stoppingToken).ConfigureAwait(false);
                        continue;
                    }

                    var messages = response.Value;

                    if (messages.Length == 0)
                    {
                        await Task.Delay(_options.PollingInterval, _timeProvider, stoppingToken).ConfigureAwait(false);
                        continue;
                    }

                    foreach (var message in messages)
                    {
                        var executionId = Guid.NewGuid();

                        using var __ = _logger.BeginScope(new
                        {
                            ExecutionId = executionId,
                            message.MessageId,
                        });

                        _logger.LogDebug("Storage queue trigger executing...");

                        TContext context;

                        try
                        {
                            context = await _contextFactory(executionId, message).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            // Todo: What to do with poison messages to prevent them from going around and around?
                            _logger.LogError(ex, "Failed to create trigger context from queue message");
                            continue;
                        }

                        await next(context, stoppingToken).ConfigureAwait(false);

                        _logger.LogDebug("Storage queue trigger executed");
                    }
                }
            }
            finally
            {
                _logger.LogInformation("Storage queue trigger exited");
            }
        }
    }

    public sealed class StorageQueueTrigger : ITrigger<StorageQueueTriggerContext>
    {
        private readonly StorageQueueTriggerBase<StorageQueueTriggerContext> _inner;
        private readonly QueueClient _queueClient;
        private readonly TimeProvider _timeProvider;

        public StorageQueueTrigger(
            QueueServiceClient queueServiceClient,
            string queueName,
            StorageQueueTriggerOptions options,
            TimeProvider timeProvider,
            ILogger<StorageQueueTrigger> logger)
        {
            ArgumentNullException.ThrowIfNull(queueServiceClient);
            ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(logger);

            _inner = new StorageQueueTriggerBase<StorageQueueTriggerContext>(
                _queueClient = queueServiceClient.GetQueueClient(queueName),
                options,
                CreateContext,
                timeProvider,
                logger);

            _timeProvider = timeProvider;
        }

        public Task Execute(WorkerDelegate<StorageQueueTriggerContext> next, CancellationToken stoppingToken) =>
            _inner.Execute(next, stoppingToken);

        private Task<StorageQueueTriggerContext> CreateContext(Guid executionId, QueueMessage queueMessage) =>
            Task.FromResult(
                new StorageQueueTriggerContext(
                    executionId,
                    _timeProvider.GetUtcNow(),
                    queueMessage,
                    async ct => await _queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt, ct).ConfigureAwait(false)));
    }

    public sealed class StorageQueueTrigger<T> : ITrigger<StorageQueueTriggerContext<T>>
    {
        private readonly StorageQueueTriggerBase<StorageQueueTriggerContext<T>> _inner;
        private readonly QueueClient _queueClient;
        private readonly StorageQueueMessageDeserializer<T> _deserializer;
        private readonly TimeProvider _timeProvider;

        public StorageQueueTrigger(
            QueueServiceClient queueServiceClient,
            string queueName,
            StorageQueueTriggerOptions options,
            StorageQueueMessageDeserializer<T> deserializer,
            TimeProvider timeProvider,
            ILogger<StorageQueueTrigger<T>> logger)
        {
            ArgumentNullException.ThrowIfNull(queueServiceClient);
            ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(deserializer);
            ArgumentNullException.ThrowIfNull(timeProvider);
            ArgumentNullException.ThrowIfNull(logger);

            _inner = new StorageQueueTriggerBase<StorageQueueTriggerContext<T>>(
                _queueClient = queueServiceClient.GetQueueClient(queueName),
                options,
                CreateContext,
                timeProvider,
                logger);

            _deserializer = deserializer;
            _timeProvider = timeProvider;
        }

        public Task Execute(WorkerDelegate<StorageQueueTriggerContext<T>> next, CancellationToken stoppingToken) =>
            _inner.Execute(next, stoppingToken);

        private async Task<StorageQueueTriggerContext<T>> CreateContext(Guid executionId, QueueMessage queueMessage) =>
            new(
                executionId,
                _timeProvider.GetUtcNow(),
                await _deserializer(queueMessage).ConfigureAwait(false),
                queueMessage,
                async ct => await _queueClient.DeleteMessageAsync(queueMessage.MessageId, queueMessage.PopReceipt, ct).ConfigureAwait(false));
    }
}
