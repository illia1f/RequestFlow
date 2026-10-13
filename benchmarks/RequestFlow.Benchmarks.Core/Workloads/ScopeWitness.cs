namespace RequestFlow.Benchmarks.Core;

public sealed class ScopeWitness : IDisposable
{
    public bool IsDisposed { get; private set; }

    public void Dispose() => IsDisposed = true;
}
