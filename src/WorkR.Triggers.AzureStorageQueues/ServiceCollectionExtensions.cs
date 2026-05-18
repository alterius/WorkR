using Azure.Storage.Queues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkR.Middleware;

namespace WorkR.Triggers.AzureStorageQueues
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddStorageQueueTrigger(
            this IServiceCollection services,
            Func<IServiceProvider, QueueClient> queueClientFactory,
            WorkerPipelineBuilderDelegate<StorageQueueTrigger, StorageQueueTriggerContext> builder,
            Action<StorageQueueTriggerConfig>? configure = null)
        {
            services.TryAddSingleton(TimeProvider.System);

            var config = new StorageQueueTriggerConfig();
            configure?.Invoke(config);

            return services.AddWorker(
                sp => ActivatorUtilities.CreateInstance<StorageQueueTrigger>(sp, queueClientFactory(sp), config),
                builder,
                static mw => mw
                    .UseFireAndForget()
                    .UseScope()
                    .UseErrorHandling<Exception>(ex => ex is not OperationCanceledException));
        }

        public static IServiceCollection AddStorageQueueTrigger<TWorker>(
            this IServiceCollection services,
            Func<IServiceProvider, QueueClient> queueClientFactory,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null,
            Action<StorageQueueTriggerConfig>? configure = null)
                where TWorker : IWorker<StorageQueueTriggerContext>
        {
            return services.AddStorageQueueTrigger(
                queueClientFactory,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware),
                configure);
        }

        public static IServiceCollection AddStorageQueueTrigger<T>(
            this IServiceCollection services,
            Func<IServiceProvider, QueueClient> queueClientFactory,
            WorkerPipelineBuilderDelegate<StorageQueueTrigger<T>, StorageQueueTriggerContext<T>> builder,
            Action<StorageQueueTriggerConfig>? configure = null,
            Func<IServiceProvider, IStorageQueueMessageDeserializer<T>>? deserializerFactory = null)
        {
            services.TryAddSingleton(TimeProvider.System);

            var config = new StorageQueueTriggerConfig();
            configure?.Invoke(config);

            return services.AddWorker(
                sp =>
                {
                    var deserialzier = deserializerFactory?.Invoke(sp)
                        ?? new JsonStorageQueueMessageDeserializer<T>();

                    return ActivatorUtilities.CreateInstance<StorageQueueTrigger<T>>(sp, queueClientFactory(sp), config, deserialzier);
                },
                builder,
                static mw => mw
                    .UseFireAndForget()
                    .UseScope()
                    .UseErrorHandling<Exception>(ex => ex is not OperationCanceledException));
        }

        public static IServiceCollection AddStorageQueueTrigger<T, TWorker>(
            this IServiceCollection services,
            Func<IServiceProvider, QueueClient> queueClientFactory,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null,
            Action<StorageQueueTriggerConfig>? configure = null,
            Func<IServiceProvider, IStorageQueueMessageDeserializer<T>>? deserializerFactory = null)
                where TWorker : IWorker<StorageQueueTriggerContext<T>>
        {
            return services.AddStorageQueueTrigger(
                queueClientFactory,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware),
                configure,
                deserializerFactory);
        }
    }
}
