namespace RequestFlow.Benchmarks.Core;

public sealed class DependencyState(ScopeWitness scope, ShortCircuitSettings settings)
{
    public ScopeWitness Scope { get; } = scope;
    public ShortCircuitSettings Settings { get; } = settings;
}

public abstract class DependencyComponent : WorkloadComponent
{
    public DependencyState State { get; }
    public string Role { get; }

    protected DependencyComponent(DependencyState state, string role) : base(state.Scope)
    {
        State = state;
        Role = role;
        state.Settings.Trace?.Constructed.Add(this);
    }

    protected void Enter(ShortCircuitMessage message, CancellationToken token) => Observe(message, Role, token);

    public override void Dispose()
    {
        State.Settings.Trace?.Disposed.Add(this);
        base.Dispose();
    }
}
