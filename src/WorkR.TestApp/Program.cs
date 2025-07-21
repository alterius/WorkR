using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

            builder.Services.TryAddSingleton(TimeProvider.System);

            builder.Services.AddWorker<HelloWorldWorker>()
                .AddDelay(TimeSpan.FromSeconds(5))
                .AddErrorHandling();

            var host = builder.Build();
            host.Run();
        }
    }
}
