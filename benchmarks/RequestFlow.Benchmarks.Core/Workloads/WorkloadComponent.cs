namespace RequestFlow.Benchmarks.Core;

public abstract class WorkloadComponent(ScopeWitness scope) : IDisposable
{
    public ScopeWitness Scope { get; } = scope;

    public bool IsDisposed { get; private set; }

    protected void Observe(WorkloadMessage message, string name, CancellationToken token)
        => message.Trace?.Record(new ExecutionRecord(name, this, Scope, token));

    public virtual void Dispose() => IsDisposed = true;
}
