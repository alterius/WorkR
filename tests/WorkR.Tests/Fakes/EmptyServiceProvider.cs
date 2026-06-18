namespace WorkR.Tests
{
    /// <summary>An <see cref="IServiceProvider"/> that resolves nothing; for pipelines that don't touch DI.</summary>
    internal sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();

        public object? GetService(Type serviceType) => null;
    }
}
