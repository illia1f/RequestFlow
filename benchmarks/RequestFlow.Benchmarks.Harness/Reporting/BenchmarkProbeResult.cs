namespace RequestFlow.Benchmarks.Harness;

/// <summary>
/// The counts a passed probe checked before timing.
/// </summary>
public sealed record BenchmarkProbeResult(int Classes, int ParameterSets, int Cases);
