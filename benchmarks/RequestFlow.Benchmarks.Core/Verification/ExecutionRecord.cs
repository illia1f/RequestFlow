namespace RequestFlow.Benchmarks.Core;

public sealed record ExecutionRecord(
    string Name, WorkloadComponent Component, ScopeWitness Scope, CancellationToken Token);
