using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace WorkR
{
    internal static class WorkRDiagnostics
    {
        private static readonly string? Version =
            typeof(WorkRDiagnostics).Assembly.GetName().Version?.ToString();

        internal static readonly ActivitySource Source = new("WorkR", Version);

        internal static readonly Meter Meter = new("WorkR", Version);

        internal static readonly Histogram<double> ExecutionDuration = Meter.CreateHistogram<double>(
            "workr.execution.duration",
            unit: "s",
            description: "Duration of worker pipeline executions."
#if NET9_0_OR_GREATER
            , advice: new InstrumentAdvice<double>
            {
                // Seconds-scale boundaries for pipeline executions, which can run
                // from sub-second to several minutes. Mirrors the shape of
                // ASP.NET Core's http.server.request.duration set, extended for
                // longer-running work.
                HistogramBucketBoundaries =
                [
                    0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1,
                    2.5, 5, 7.5, 10, 30, 60, 120, 300
                ]
            }
#endif
            );
    }
}
