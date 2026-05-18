using Azure.Storage.Queues;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCrontab;
using WorkR.Triggers.AzureStorageQueues;
using WorkR.Triggers.Timers;

namespace WorkR.Samples.AzureStorageQueue
{
    internal partial class Program
    {
        static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Logging.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
            });

            const string queueName = "test-messages";

            builder.Services.AddAzureClients(azureBuilder =>
            {
                azureBuilder.AddQueueServiceClient(
                    builder.Configuration.GetConnectionString("StorageConnectionString"));
            });

            builder.Services.AddSingleton(sp =>
            {
                var serviceClient = sp.GetRequiredService<QueueServiceClient>();
                var queueClient = serviceClient.GetQueueClient(queueName);
                queueClient.CreateIfNotExists();
                return queueClient;
            });

            builder.Services.AddScheduledWorker<SendTestMessageWorker>(
                "*/5 * * * * *",
                parseOptions: new CrontabSchedule.ParseOptions
                {
                    IncludingSeconds = true
                });

            builder.Services.AddStorageQueueTrigger<TestMessage, LogMessageWorker<TestMessage>>(
                sp => sp.GetRequiredService<QueueClient>());

            var host = builder.Build();
            host.Run();
        }
    }
}
