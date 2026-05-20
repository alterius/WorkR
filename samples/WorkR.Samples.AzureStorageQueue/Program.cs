using Azure.Storage.Queues;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.Azurite;
using WorkR.Triggers.AzureStorageQueues;
using WorkR.Triggers.Timers;

namespace WorkR.Samples.AzureStorageQueue
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var container = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.35.0")
                .Build();

            await container.StartAsync();

            await using (container)
            {
                const string queueName = "test-messages";

                var builder = Host.CreateApplicationBuilder(args);

                builder.Logging.AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
                });

                builder.Services.AddAzureClients(azureBuilder =>
                {
                    azureBuilder.AddQueueServiceClient(container.GetConnectionString());
                });

                builder.Services.AddSingleton(sp =>
                    sp.GetRequiredService<QueueServiceClient>()
                        .GetQueueClient(queueName));

                builder.Services.AddScheduledWorker<SendTestMessageWorker>(
                    "*/5 * * * * *",
                    includeSeconds: true);

                builder.Services.AddStorageQueueWorker<TestMessage, LogMessageWorker<TestMessage>>(
                    sp => sp.GetRequiredService<QueueServiceClient>(),
                    queueName);

                var host = builder.Build();

                await host.Services.GetRequiredService<QueueClient>()
                    .CreateIfNotExistsAsync();

                await host.RunAsync();
            }
        }
    }
}
