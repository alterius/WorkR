using Azure.Storage.Queues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WorkR.Middleware;

namespace WorkR.Triggers.AzureStorageQueues
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddStorageQueueWorker(
            this IServiceCollection services,
            Func<IServiceProvider, QueueServiceClient> queueServiceClientFactory,
            string queueName,
            WorkerPipelineBuilderDelegate<StorageQueueTrigger, StorageQueueTriggerContext> builder,
            Action<StorageQueueTriggerOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(queueServiceClientFactory);
            ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
            ArgumentNullException.ThrowIfNull(builder);

            services.TryAddSingleton(TimeProvider.System);

            var options = new StorageQueueTriggerOptions();
            configure?.Invoke(options);

            return services.AddWorker(
                sp => new StorageQueueTrigger(
                    queueServiceClientFactory(sp),
                    queueName,
                    options,
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<ILogger<StorageQueueTrigger>>()),
                builder,
                static mw => mw
                    .UseFireAndForget()
                    .UseScope());
        }

        public static IServiceCollection AddStorageQueueWorker<TWorker>(
            this IServiceCollection services,
            Func<IServiceProvider, QueueServiceClient> queueServiceClientFactory,
            string queueName,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null,
            Action<StorageQueueTriggerOptions>? configure = null)
                where TWorker : IWorker<StorageQueueTriggerContext>
        {
            return services.AddStorageQueueWorker(
                queueServiceClientFactory,
                queueName,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware),
                configure);
        }

        public static IServiceCollection AddStorageQueueWorker<T>(
            this IServiceCollection services,
            Func<IServiceProvider, QueueServiceClient> queueServiceClientFactory,
            string queueName,
            WorkerPipelineBuilderDelegate<StorageQueueTrigger<T>, StorageQueueTriggerContext<T>> builder,
            Action<StorageQueueTriggerOptions>? configure = null,
            Func<IServiceProvider, StorageQueueMessageDeserializer<T>>? deserializerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(queueServiceClientFactory);
            ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
            ArgumentNullException.ThrowIfNull(builder);

            services.TryAddSingleton(TimeProvider.System);

            var options = new StorageQueueTriggerOptions();
            configure?.Invoke(options);

            return services.AddWorker(
                sp => new StorageQueueTrigger<T>(
                    queueServiceClientFactory(sp),
                    queueName,
                    options,
                    deserializerFactory?.Invoke(sp) ?? StorageQueueMessageDeserializers.Json<T>(),
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<ILogger<StorageQueueTrigger<T>>>()),
                builder,
                static mw => mw
                    .UseFireAndForget()
                    .UseScope());
        }

        public static IServiceCollection AddStorageQueueWorker<T, TWorker>(
            this IServiceCollection services,
            Func<IServiceProvider, QueueServiceClient> queueServiceClientFactory,
            string queueName,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null,
            Action<StorageQueueTriggerOptions>? configure = null,
            Func<IServiceProvider, StorageQueueMessageDeserializer<T>>? deserializerFactory = null)
                where TWorker : IWorker<StorageQueueTriggerContext<T>>
        {
            return services.AddStorageQueueWorker(
                queueServiceClientFactory,
                queueName,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware),
                configure,
                deserializerFactory);
        }
    }
}
