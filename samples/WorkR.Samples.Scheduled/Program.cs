using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkR.Triggers.Timers;

namespace WorkR.Samples.Scheduled
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

            builder.Services.AddScheduledWorker<HelloWorldWorker>(
                "*/5 * * * * *",
                includeSeconds: true);

            var host = builder.Build();
            host.Run();
        }
    }
}
