using System.Diagnostics;

namespace WorkR
{
    /// <summary>
    /// Holds the shared <see cref="ActivitySource"/> for WorkR tracing.
    /// </summary>
    internal static class WorkRDiagnostics
    {
        /// <summary>
        /// The activity source named <c>"WorkR"</c> — a stable public contract — used to emit one
        /// span per pipeline execution.
        /// </summary>
        internal static readonly ActivitySource Source = new(
            "WorkR",
            typeof(WorkRDiagnostics).Assembly.GetName().Version?.ToString());
    }
}
