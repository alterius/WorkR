using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WorkR.Samples.CustomTrigger
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

            builder.Services.AddSingleton(TimeProvider.System);

            builder.Services.AddWorker<RandomNumberTrigger, ValueTriggerContext<int>>(
                sp => new RandomNumberTrigger(sp.GetRequiredService<TimeProvider>()),
                trigger => trigger
                    .AddWorker<UnpackNumberWorker, int>()
                    .AddWorker<NumberTimesTenWorker, int>(middleware: middleware => middleware.UseErrorHandling<Exception>())
                    .AddWorker<NumberTimesTenWorker, int>()
                    .AddWorker<ConvertToStringWorker, string>()
                    .AddWorker<PrintStringWorker>());

            var host = builder.Build();
            host.Run();
        }
    }
}
