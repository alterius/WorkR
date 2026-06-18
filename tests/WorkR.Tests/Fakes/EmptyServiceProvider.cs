using Microsoft.Extensions.DependencyInjection;

namespace WorkR.Tests
{
    /// <summary>
    /// An <see cref="IServiceProvider"/> that resolves nothing; for pipelines that don't touch DI.
    /// Supports scope creation so pipelines that open an async scope per execution can run.
    /// </summary>
    internal sealed class EmptyServiceProvider : IServiceProvider, IServiceScopeFactory, IServiceScope
    {
        public static readonly EmptyServiceProvider Instance = new();

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IServiceScopeFactory) ? this : null;

        public IServiceScope CreateScope() => this;

        public IServiceProvider ServiceProvider => this;

        public void Dispose()
        {
        }
    }
}
