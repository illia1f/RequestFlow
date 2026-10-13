using System.Text.Json;

namespace RequestFlow.Benchmarks.Harness;

internal sealed record BenchmarkRunManifest(
    string Suite,
    string Tier,
    DateTimeOffset StartedUtc,
    DateTimeOffset? EndedUtc,
    string[] Arguments,
    BenchmarkSource Source,
    BenchmarkSource? FinalSource,
    bool SourceUnchanged,
    BenchmarkHostEnvironment HostEnvironment,
    IReadOnlyDictionary<string, string> Packages,
    BenchmarkJobDescription[] EffectiveJobs,
    BenchmarkProbeResult? Probe,
    string[] Warnings,
    string Status,
    string? Error,
    bool PublicationCandidate)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    internal static BenchmarkRunManifest Start(
        BenchmarkRun run,
        BenchmarkSource source,
        BenchmarkHostEnvironment environment,
        IReadOnlyDictionary<string, string> packages,
        BenchmarkJobDescription[] effectiveJobs)
        => new(
            run.Suite,
            run.Tier.ToString().ToLowerInvariant(),
            DateTimeOffset.UtcNow,
            null,
            run.OriginalArguments,
            source,
            null,
            false,
            environment,
            packages,
            effectiveJobs,
            null,
            [],
            BenchmarkRunStatus.Running.Name,
            null,
            false);

    internal BenchmarkRunManifest WithProbe(BenchmarkProbeResult probe) => this with { Probe = probe };

    /// <summary>
    /// Records the outcome.
    /// Only a completed full run with a passed probe, from clean, unchanged source without measurement overrides or runtime variables, is a publication candidate.
    /// </summary>
    internal BenchmarkRunManifest Finish(
        BenchmarkRunStatus status,
        BenchmarkJobDescription[] effectiveJobs,
        string[] warnings,
        BenchmarkSource finalSource,
        bool hasMeasurementOverrides)
    {
        bool unchanged = Source.IsSameAs(finalSource);
        string[] executionVariables = HostEnvironment.ExecutionVariables;
        List<string> allWarnings = [.. warnings];

        if (!unchanged)
            allWarnings.Add("Source revalidation failed: HEAD or the Git status of a build input changed during the run, or Git could not identify the source. This run is not a publication candidate.");

        if (executionVariables.Length > 0)
            allWarnings.Add($"Runtime environment variables are set: {string.Join(", ", executionVariables)}. This run is not a publication candidate.");

        return this with
        {
            EndedUtc = DateTimeOffset.UtcNow,
            FinalSource = finalSource,
            SourceUnchanged = unchanged,
            EffectiveJobs = effectiveJobs,
            Warnings = [.. allWarnings],
            Status = status.Name,
            Error = status.Error,
            PublicationCandidate = status == BenchmarkRunStatus.Completed && Tier == "full" &&
                Source.IsIdentified && !Source.IsDirty && unchanged && !hasMeasurementOverrides &&
                executionVariables.Length == 0 && Probe is not null,
        };
    }

    internal void Write(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, SerializerOptions));
}
