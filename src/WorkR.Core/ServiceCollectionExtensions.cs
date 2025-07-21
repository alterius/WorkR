using Microsoft.Extensions.DependencyInjection;

namespace WorkR
{
    public static class ServiceCollectionExtensions
    {
        public static WorkerRegistrationBuilder AddWorker<T>(this IServiceCollection services)
            where T : Worker
        {
            var builder = new WorkerRegistrationBuilder();

            services.AddHostedService(sp =>
                ActivatorUtilities.CreateInstance<T>(sp, builder.Build(sp)));

            return builder;
        }
    }
}
