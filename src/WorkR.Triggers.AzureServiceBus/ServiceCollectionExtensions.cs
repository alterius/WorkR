using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WorkR.Middleware;

namespace WorkR.Triggers.AzureServiceBus
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddServiceBusTrigger(
            this IServiceCollection services,
            Func<IServiceProvider, ServiceBusClient> clientFactory,
            string queueName,
            WorkerPipelineBuilderDelegate<ServiceBusTrigger, ServiceBusTriggerContext> builder,
            ServiceBusProcessorOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(clientFactory);
            ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
            ArgumentNullException.ThrowIfNull(builder);

            services.TryAddSingleton(TimeProvider.System);

            return services.AddWorker(
                sp => new ServiceBusTrigger(
                    clientFactory(sp),
                    queueName,
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<ILogger<ServiceBusTrigger>>(),
                    options),
                builder,
                static mw => mw
                    .UseFireAndForget()
                    .UseScope()
                    .UseErrorHandling<Exception>(ex => ex is not OperationCanceledException));
        }

        public static IServiceCollection AddServiceBusTrigger<TWorker>(
            this IServiceCollection services,
            Func<IServiceProvider, ServiceBusClient> clientFactory,
            string queueName,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null,
            ServiceBusProcessorOptions? options = null)
                where TWorker : IWorker<ServiceBusTriggerContext>
        {
            return services.AddServiceBusTrigger(
                clientFactory,
                queueName,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware),
                options);
        }

        public static IServiceCollection AddServiceBusTrigger(
            this IServiceCollection services,
            Func<IServiceProvider, ServiceBusClient> clientFactory,
            string topicName,
            string subscriptionName,
            WorkerPipelineBuilderDelegate<ServiceBusTrigger, ServiceBusTriggerContext> builder,
            ServiceBusProcessorOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(clientFactory);
            ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
            ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
            ArgumentNullException.ThrowIfNull(builder);

            services.TryAddSingleton(TimeProvider.System);

            return services.AddWorker(
                sp => new ServiceBusTrigger(
                    clientFactory(sp),
                    topicName,
                    subscriptionName,
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<ILogger<ServiceBusTrigger>>(),
                    options),
                builder,
                static mw => mw
                    .UseFireAndForget()
                    .UseScope()
                    .UseErrorHandling<Exception>(ex => ex is not OperationCanceledException));
        }

        public static IServiceCollection AddServiceBusTrigger<TWorker>(
            this IServiceCollection services,
            Func<IServiceProvider, ServiceBusClient> clientFactory,
            string topicName,
            string subscriptionName,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null,
            ServiceBusProcessorOptions? options = null)
                where TWorker : IWorker<ServiceBusTriggerContext>
        {
            return services.AddServiceBusTrigger(
                clientFactory,
                topicName,
                subscriptionName,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware),
                options);
        }

        public static IServiceCollection AddServiceBusTrigger<T>(
            this IServiceCollection services,
            Func<IServiceProvider, ServiceBusClient> clientFactory,
            string queueName,
            WorkerPipelineBuilderDelegate<ServiceBusTrigger<T>, ServiceBusTriggerContext<T>> builder,
            ServiceBusProcessorOptions? options = null,
            Func<IServiceProvider, ServiceBusMessageDeserializer<T>>? deserializerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(clientFactory);
            ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
            ArgumentNullException.ThrowIfNull(builder);

            services.TryAddSingleton(TimeProvider.System);

            return services.AddWorker(
                sp => new ServiceBusTrigger<T>(
                    clientFactory(sp),
                    queueName,
                    deserializerFactory?.Invoke(sp) ?? ServiceBusMessageDeserializers.Json<T>(),
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<ILogger<ServiceBusTrigger<T>>>(),
                    options),
                builder,
                static mw => mw
                    .UseFireAndForget()
                    .UseScope()
                    .UseErrorHandling<Exception>(ex => ex is not OperationCanceledException));
        }

        public static IServiceCollection AddServiceBusTrigger<T, TWorker>(
            this IServiceCollection services,
            Func<IServiceProvider, ServiceBusClient> clientFactory,
            string queueName,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null,
            ServiceBusProcessorOptions? options = null,
            Func<IServiceProvider, ServiceBusMessageDeserializer<T>>? deserializerFactory = null)
                where TWorker : IWorker<ServiceBusTriggerContext<T>>
        {
            return services.AddServiceBusTrigger(
                clientFactory,
                queueName,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware),
                options,
                deserializerFactory);
        }

        public static IServiceCollection AddServiceBusTrigger<T>(
            this IServiceCollection services,
            Func<IServiceProvider, ServiceBusClient> clientFactory,
            string topicName,
            string subscriptionName,
            WorkerPipelineBuilderDelegate<ServiceBusTrigger<T>, ServiceBusTriggerContext<T>> builder,
            ServiceBusProcessorOptions? options = null,
            Func<IServiceProvider, ServiceBusMessageDeserializer<T>>? deserializerFactory = null)
        {
            ArgumentNullException.ThrowIfNull(clientFactory);
            ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
            ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
            ArgumentNullException.ThrowIfNull(builder);

            services.TryAddSingleton(TimeProvider.System);

            return services.AddWorker(
                sp => new ServiceBusTrigger<T>(
                    clientFactory(sp),
                    topicName,
                    subscriptionName,
                    deserializerFactory?.Invoke(sp) ?? ServiceBusMessageDeserializers.Json<T>(),
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<ILogger<ServiceBusTrigger<T>>>(),
                    options),
                builder,
                static mw => mw
                    .UseFireAndForget()
                    .UseScope()
                    .UseErrorHandling<Exception>(ex => ex is not OperationCanceledException));
        }

        public static IServiceCollection AddServiceBusTrigger<T, TWorker>(
            this IServiceCollection services,
            Func<IServiceProvider, ServiceBusClient> clientFactory,
            string topicName,
            string subscriptionName,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null,
            ServiceBusProcessorOptions? options = null,
            Func<IServiceProvider, ServiceBusMessageDeserializer<T>>? deserializerFactory = null)
                where TWorker : IWorker<ServiceBusTriggerContext<T>>
        {
            return services.AddServiceBusTrigger(
                clientFactory,
                topicName,
                subscriptionName,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware),
                options,
                deserializerFactory);
        }
    }
}
