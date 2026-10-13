namespace RequestFlow.Benchmarks.Core;

public sealed record ShortCircuitSettings(int BufferBytes, ConstructionTrace? Trace = null);

public sealed class ConstructionTrace
{
    public List<DependencyComponent> Constructed { get; } = [];
    public List<DependencyComponent> Disposed { get; } = [];
}
