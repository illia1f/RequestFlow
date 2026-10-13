namespace RequestFlow.Benchmarks.Core;

public sealed record BenchmarkObservation(
    Type BenchmarkType,
    string Method,
    IReadOnlyDictionary<string, object?> Parameters,
    object? Result,
    IReadOnlyList<ExecutionRecord>? Trace = null);
