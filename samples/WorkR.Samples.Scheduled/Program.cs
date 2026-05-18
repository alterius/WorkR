using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCrontab;
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
                parseOptions: new CrontabSchedule.ParseOptions
                {
                    IncludingSeconds = true
                });

            var host = builder.Build();
            host.Run();
        }
    }
}
