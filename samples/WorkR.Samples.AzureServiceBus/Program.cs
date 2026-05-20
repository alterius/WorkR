using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCrontab;
using Testcontainers.ServiceBus;
using WorkR.Triggers.AzureServiceBus;
using WorkR.Triggers.Timers;

namespace WorkR.Samples.AzureServiceBus
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var container = new ServiceBusBuilder("mcr.microsoft.com/azure-messaging/servicebus-emulator:2.0.0")
                .WithAcceptLicenseAgreement(true)
                .WithConfig("sbconfig.json")
                .Build();

            await container.StartAsync();

            await using (container)
            {
                var builder = Host.CreateApplicationBuilder(args);

                builder.Logging.AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
                });

                builder.Services.AddAzureClients(azureBuilder =>
                {
                    azureBuilder.AddServiceBusClient(container.GetConnectionString());
                });

                const string queueName = "test-messages";

                builder.Services.AddSingleton(sp =>
                {
                    var client = sp.GetRequiredService<ServiceBusClient>();
                    return client.CreateSender(queueName);
                });

                builder.Services.AddScheduledWorker<SendTestMessageWorker>(
                    "*/5 * * * * *",
                    parseOptions: new CrontabSchedule.ParseOptions
                    {
                        IncludingSeconds = true
                    });

                builder.Services.AddServiceBusTrigger<TestMessage, LogMessageWorker<TestMessage>>(
                    sp => sp.GetRequiredService<ServiceBusClient>(),
                    queueName);

                var host = builder.Build();
                await host.RunAsync();
            }
        }
    }
}
