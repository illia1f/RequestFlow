namespace RequestFlow.Benchmarks.Core;

/// <summary>
/// The scope lifetime that the probe checks for every method of a benchmark class.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class BenchmarkScopeAttribute(ScopeLifetime lifetime) : Attribute
{
    public ScopeLifetime Lifetime { get; } = lifetime;
}
