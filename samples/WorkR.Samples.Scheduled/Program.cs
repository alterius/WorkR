using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkR.Triggers.Timers;

namespace WorkR.TestApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Logging.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ";
            });

            builder.Services.AddScheduledWorker<HelloWorldWorker>("*/5 * * * * *");

            builder.Services.AddWorker<RandomNumberTrigger, int>(
                _ => new RandomNumberTrigger(),
                trigger => trigger.AddWorker<NumberTimesTenWorker, int>(middleware: middleware => middleware.UseErrorHandling<Exception>())
                    .AddWorker<NumberTimesTenWorker, int>()
                    .AddWorker<ConvertToStringWorker, string>()
                    .AddWorker<PrintStringWorker>());

            var host = builder.Build();
            host.Run();
        }
    }
}
