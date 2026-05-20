using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WorkR.Middleware;

namespace WorkR.Triggers.AzureServiceBus
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddServiceBusWorker(
            this IServiceCollection services,
            Func<IServiceProvider, ServiceBusClient> clientFactory,
            string queueName,
            WorkerPipelineBuilderDelegate<ServiceBusTrigger, ServiceBusTriggerContext> builder,
            Action<ServiceBusProcessorOptions>? configure = null)
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
                    CreateOptions(configure)),
                builder,
                static mw => mw
                    .UseFireAndForget()
                    .UseScope()
                    .UseErrorHandling<Exception>(ex => ex is not OperationCanceledException));
        }

        public static IServiceCollection AddServiceBusWorker<TWorker>(
            this IServiceCollection services,
            Func<IServiceProvider, ServiceBusClient> clientFactory,
            string queueName,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null,
            Action<ServiceBusProcessorOptions>? configure = null)
                where TWorker : IWorker<ServiceBusTriggerContext>
        {
            return services.AddServiceBusWorker(
                clientFactory,
                queueName,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware),
                configure);
        }

        public static IServiceCollection AddServiceBusWorker(
            this IServiceCollection services,
            Func<IServiceProvider, ServiceBusClient> clientFactory,
            string topicName,
            string subscriptionName,
            WorkerPipelineBuilderDelegate<ServiceBusTrigger, ServiceBusTriggerContext> builder,
            Action<ServiceBusProcessorOptions>? configure = null)
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
                    CreateOptions(configure)),
                builder,
                static mw => mw
                    .UseFireAndForget()
                    .UseScope()
                    .UseErrorHandling<Exception>(ex => ex is not OperationCanceledException));
        }

        public static IServiceCollection AddServiceBusWorker<TWorker>(
            this IServiceCollection services,
            Func<IServiceProvider, ServiceBusClient> clientFactory,
            string topicName,
            string subscriptionName,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null,
            Action<ServiceBusProcessorOptions>? configure = null)
                where TWorker : IWorker<ServiceBusTriggerContext>
        {
            return services.AddServiceBusWorker(
                clientFactory,
                topicName,
                subscriptionName,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware),
                configure);
        }

        public static IServiceCollection AddServiceBusWorker<T>(
            this IServiceCollection services,
            Func<IServiceProvider, ServiceBusClient> clientFactory,
            string queueName,
            WorkerPipelineBuilderDelegate<ServiceBusTrigger<T>, ServiceBusTriggerContext<T>> builder,
            Action<ServiceBusProcessorOptions>? configure = null,
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
                    CreateOptions(configure)),
                builder,
                static mw => mw
                    .UseFireAndForget()
                    .UseScope()
                    .UseErrorHandling<Exception>(ex => ex is not OperationCanceledException));
        }

        public static IServiceCollection AddServiceBusWorker<T, TWorker>(
            this IServiceCollection services,
            Func<IServiceProvider, ServiceBusClient> clientFactory,
            string queueName,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null,
            Action<ServiceBusProcessorOptions>? configure = null,
            Func<IServiceProvider, ServiceBusMessageDeserializer<T>>? deserializerFactory = null)
                where TWorker : IWorker<ServiceBusTriggerContext<T>>
        {
            return services.AddServiceBusWorker(
                clientFactory,
                queueName,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware),
                configure,
                deserializerFactory);
        }

        public static IServiceCollection AddServiceBusWorker<T>(
            this IServiceCollection services,
            Func<IServiceProvider, ServiceBusClient> clientFactory,
            string topicName,
            string subscriptionName,
            WorkerPipelineBuilderDelegate<ServiceBusTrigger<T>, ServiceBusTriggerContext<T>> builder,
            Action<ServiceBusProcessorOptions>? configure = null,
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
                    CreateOptions(configure)),
                builder,
                static mw => mw
                    .UseFireAndForget()
                    .UseScope()
                    .UseErrorHandling<Exception>(ex => ex is not OperationCanceledException));
        }

        public static IServiceCollection AddServiceBusWorker<T, TWorker>(
            this IServiceCollection services,
            Func<IServiceProvider, ServiceBusClient> clientFactory,
            string topicName,
            string subscriptionName,
            ServiceLifetime workerLifetime = ServiceLifetime.Transient,
            Action<MiddlewarePipelineBuilder>? middleware = null,
            Action<ServiceBusProcessorOptions>? configure = null,
            Func<IServiceProvider, ServiceBusMessageDeserializer<T>>? deserializerFactory = null)
                where TWorker : IWorker<ServiceBusTriggerContext<T>>
        {
            return services.AddServiceBusWorker(
                clientFactory,
                topicName,
                subscriptionName,
                builder => builder.AddWorker<TWorker>(workerLifetime, middleware),
                configure,
                deserializerFactory);
        }

        private static ServiceBusProcessorOptions? CreateOptions(Action<ServiceBusProcessorOptions>? configure)
        {
            if (configure is null)
            {
                return null;
            }
            
            var options = new ServiceBusProcessorOptions();
            configure(options);

            return options;
        }
    }
}
