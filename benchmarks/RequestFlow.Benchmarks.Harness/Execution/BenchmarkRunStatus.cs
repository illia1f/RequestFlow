using BenchmarkDotNet.Reports;

namespace RequestFlow.Benchmarks.Harness;

internal sealed record BenchmarkRunStatus(string Name, string? Error)
{
    internal static readonly BenchmarkRunStatus Completed = new("completed", null);
    internal static readonly BenchmarkRunStatus Running = new("running", null);

    internal static BenchmarkRunStatus Failed(string error) => new("failed", error);

    internal static BenchmarkRunStatus Determine(Summary[] summaries)
    {
        if (summaries.Length == 0)
            return Failed("No benchmark matched the filter. Check the pattern with --list flat.");

        bool hasFailures = summaries.Any(summary => summary.HasCriticalValidationErrors || summary.Reports.Length == 0) ||
            summaries.SelectMany(summary => summary.Reports).Any(report =>
                !report.Success || report.ExecuteResults.Any(result => result.ExitCode != 0));

        return hasFailures ? Failed("BenchmarkDotNet did not complete every selected benchmark.") : Completed;
    }
}
