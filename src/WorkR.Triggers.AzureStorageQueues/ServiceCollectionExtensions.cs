using Azure.Storage.Queues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WorkR.Middleware;

namespace WorkR.Triggers.AzureStorageQueues
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddStorageQueueTrigger(
            this IServiceCollection services,
            Func<IServiceProvider, QueueServiceClient> queueServiceClientFactory,
            string queueName,
            WorkerPipelineBuilderDelegate<StorageQueueTrigger, StorageQueueTriggerContext> builder,
            Action<StorageQueueTriggerConfig>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(queueServiceClientFactory);
            ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
            ArgumentNullException.ThrowIfNull(builder);

            services.TryAddSingleton(TimeProvider.System);

            var config = new StorageQueueTriggerConfig();
            configure?.Invoke(config);

            return services.AddWorker(
                sp => new StorageQueueTrigger(
                    queueServiceClientFactory(sp),
                    queueName,
                    config,
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<ILogger<StorageQueueTrigger>>()),
                builder,
                static mw => mw
                    .UseFireAndForget()
                    .UseScope()
                    .UseErrorHandling<Exception>(ex => ex is not OperationCanceledException));
        }

        public static IServiceCollection AddStorageQueueTrigger<TWorker>(
            this IServiceCollection services,
            Func<IServiceProvider, QueueServiceClient> queueServiceClientFactory,
            string queueName,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null,
            Action<StorageQueueTriggerConfig>? configure = null)
                where TWorker : IWorker<StorageQueueTriggerContext>
        {
            return services.AddStorageQueueTrigger(
                queueServiceClientFactory,
                queueName,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware),
                configure);
        }

        public static IServiceCollection AddStorageQueueTrigger<T>(
            this IServiceCollection services,
            Func<IServiceProvider, QueueServiceClient> queueServiceClientFactory,
            string queueName,
            WorkerPipelineBuilderDelegate<StorageQueueTrigger<T>, StorageQueueTriggerContext<T>> builder,
            Action<StorageQueueTriggerConfig>? configure = null,
            Func<IServiceProvider, StorageQueueMessageDeserializer<T>>? deserializerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(queueServiceClientFactory);
            ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
            ArgumentNullException.ThrowIfNull(builder);

            services.TryAddSingleton(TimeProvider.System);

            var config = new StorageQueueTriggerConfig();
            configure?.Invoke(config);

            return services.AddWorker(
                sp => new StorageQueueTrigger<T>(
                    queueServiceClientFactory(sp),
                    queueName,
                    config,
                    deserializerFactory?.Invoke(sp) ?? StorageQueueMessageDeserializers.Json<T>(),
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<ILogger<StorageQueueTrigger<T>>>()),
                builder,
                static mw => mw
                    .UseFireAndForget()
                    .UseScope()
                    .UseErrorHandling<Exception>(ex => ex is not OperationCanceledException));
        }

        public static IServiceCollection AddStorageQueueTrigger<T, TWorker>(
            this IServiceCollection services,
            Func<IServiceProvider, QueueServiceClient> queueServiceClientFactory,
            string queueName,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null,
            Action<StorageQueueTriggerConfig>? configure = null,
            Func<IServiceProvider, StorageQueueMessageDeserializer<T>>? deserializerFactory = null)
                where TWorker : IWorker<StorageQueueTriggerContext<T>>
        {
            return services.AddStorageQueueTrigger(
                queueServiceClientFactory,
                queueName,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware),
                configure,
                deserializerFactory);
        }
    }
}
