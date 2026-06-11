using System.Diagnostics;

namespace WorkR
{
    internal static class WorkRDiagnostics
    {
        internal static readonly ActivitySource Source = new(
            "WorkR",
            typeof(WorkRDiagnostics).Assembly.GetName().Version?.ToString());
    }
}
