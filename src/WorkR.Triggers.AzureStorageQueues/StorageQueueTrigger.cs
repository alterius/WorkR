using System.Collections.Concurrent;
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
        private readonly Lazy<QueueClient> _deadLetterQueueClient;
        private readonly StorageQueueTriggerOptions _options;
        private readonly Func<Guid, QueueMessage, Task<TContext>> _contextFactory;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger _logger;

        public StorageQueueTriggerBase(
            QueueServiceClient queueServiceClient,
            string queueName,
            StorageQueueTriggerOptions options,
            Func<Guid, QueueMessage, Task<TContext>> contextFactory,
            TimeProvider timeProvider,
            ILogger logger)
        {
            _queueClient = queueServiceClient.GetQueueClient(queueName);
            _deadLetterQueueClient = new Lazy<QueueClient>(() =>
            {
                var deadLetterQueueClient = queueServiceClient.GetQueueClient($"{queueName}-poison");
                deadLetterQueueClient.CreateIfNotExists();
                return deadLetterQueueClient;
            }, LazyThreadSafetyMode.PublicationOnly);
            _options = options;
            _contextFactory = contextFactory;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task ExecuteAsync(IWorkerPipeline<TContext> workerPipeline, CancellationToken stoppingToken)
        {
            using var _ = _logger.BeginScope(
                new LogScope("QueueName", _queueClient.Name));

            _logger.LogInformation("Storage queue trigger initialised");

            var consecutiveEmptyPolls = 0;
            var consecutiveErrors = 0;
            var executingTasks = new ConcurrentDictionary<Guid, Task>();
            
            using var semaphore = new SemaphoreSlim(_options.MaxConcurrentCalls, _options.MaxConcurrentCalls);

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
                        var delay = _options.ErrorDelay(consecutiveErrors);
                        if (consecutiveErrors < int.MaxValue)
                        {
                            consecutiveErrors++;
                        }
                        await Task.Delay(delay, _timeProvider, stoppingToken).ConfigureAwait(false);
                        continue;
                    }
                    catch (HttpRequestException)
                    {
                        var delay = _options.ErrorDelay(consecutiveErrors);
                        if (consecutiveErrors < int.MaxValue)
                        {
                            consecutiveErrors++;
                        }
                        await Task.Delay(delay, _timeProvider, stoppingToken).ConfigureAwait(false);
                        continue;
                    }

                    consecutiveErrors = 0;

                    var messages = response.Value;

                    if (messages.Length == 0)
                    {
                        var delay = _options.PollingDelay(consecutiveEmptyPolls);
                        if (consecutiveEmptyPolls < int.MaxValue)
                        {
                            consecutiveEmptyPolls++;
                        }
                        await Task.Delay(delay, _timeProvider, stoppingToken).ConfigureAwait(false);
                        continue;
                    }

                    consecutiveEmptyPolls = 0;

                    foreach (var message in messages)
                    {
                        var executionId = Guid.NewGuid();

                        await semaphore.WaitAsync(stoppingToken).ConfigureAwait(false);

                        try
                        {
                            executingTasks.TryAdd(
                                executionId,
                                Task.Run(async () =>
                                {
                                    using var __ = _logger.BeginScope(
                                        new LogScope("MessageId", message.MessageId));

                                    try
                                    {
                                        var context = await CreateContextAsync(executionId, message).ConfigureAwait(false);

                                        await workerPipeline.ExecuteAsync(context, stoppingToken).ConfigureAwait(false);

                                        if (_options.AutoCompleteMessages)
                                        {
                                            await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
                                        }
                                    }
                                    catch (OperationCanceledException)
                                        when (stoppingToken.IsCancellationRequested)
                                    {
                                        // Expected shutdown
                                    }
                                    catch (Exception)
                                    {
                                        // Pipeline failures are logged by WorkerService; the trigger
                                        // only decides what happens to the message
                                        if (_options.MaxDeliveryCount > 0 && message.DequeueCount >= _options.MaxDeliveryCount)
                                        {
                                            _logger.LogWarning("Message reached max delivery count of {maxDeliveryCount}; dead lettering", _options.MaxDeliveryCount);

                                            try
                                            {
                                                await DeadLetterMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger.LogError(ex, "Failed to dead letter message");
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        semaphore.Release();
                                        executingTasks.TryRemove(executionId, out var ___);
                                    }
                                }, stoppingToken));
                        }
                        catch (Exception)
                        {
                            semaphore.Release();
                            throw;
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    await Task.WhenAll(executingTasks.Values);
                }
                catch (Exception)
                {
                }

                _logger.LogInformation("Storage queue trigger stopped");
            }
        }

        private async Task<TContext> CreateContextAsync(Guid executionId, QueueMessage message)
        {
            try
            {
                return await _contextFactory(executionId, message).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create trigger context from queue message");
                throw;
            }
        }

        internal async Task DeleteMessageAsync(QueueMessage message, CancellationToken cancellationToken)
        {
            try
            {
                await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken).ConfigureAwait(false);
            }
            catch (RequestFailedException ex)
                when (ex.Status == 404 && ex.ErrorCode == "MessageNotFound")
            {
                // Already deleted
            }
        }

        internal async Task DeadLetterMessageAsync(QueueMessage message, CancellationToken cancellationToken)
        {
            await _deadLetterQueueClient.Value.SendMessageAsync(message.Body, cancellationToken: cancellationToken).ConfigureAwait(false);
            await DeleteMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public sealed class StorageQueueTrigger : ITrigger<StorageQueueTriggerContext>
    {
        private readonly StorageQueueTriggerBase<StorageQueueTriggerContext> _inner;
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
                queueServiceClient,
                queueName,
                options,
                CreateContextAsync,
                timeProvider,
                logger);
            _timeProvider = timeProvider;
        }

        public Task ExecuteAsync(IWorkerPipeline<StorageQueueTriggerContext> workerPipeline, CancellationToken stoppingToken) =>
            _inner.ExecuteAsync(workerPipeline, stoppingToken);

        private Task<StorageQueueTriggerContext> CreateContextAsync(Guid executionId, QueueMessage queueMessage) =>
            Task.FromResult(
                new StorageQueueTriggerContext(
                    executionId,
                    _timeProvider.GetUtcNow(),
                    queueMessage,
                    ct => _inner.DeleteMessageAsync(queueMessage, ct),
                    ct => _inner.DeadLetterMessageAsync(queueMessage, ct)));
    }

    public sealed class StorageQueueTrigger<T> : ITrigger<StorageQueueTriggerContext<T>>
    {
        private readonly StorageQueueTriggerBase<StorageQueueTriggerContext<T>> _inner;
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
                queueServiceClient,
                queueName,
                options,
                CreateContextAsync,
                timeProvider,
                logger);

            _deserializer = deserializer;
            _timeProvider = timeProvider;
        }

        public Task ExecuteAsync(IWorkerPipeline<StorageQueueTriggerContext<T>> workerPipeline, CancellationToken stoppingToken) =>
            _inner.ExecuteAsync(workerPipeline, stoppingToken);

        private async Task<StorageQueueTriggerContext<T>> CreateContextAsync(Guid executionId, QueueMessage queueMessage) =>
            new(
                executionId,
                _timeProvider.GetUtcNow(),
                await _deserializer(queueMessage).ConfigureAwait(false),
                queueMessage,
                ct => _inner.DeleteMessageAsync(queueMessage, ct),
                ct => _inner.DeadLetterMessageAsync(queueMessage, ct));
    }
}
