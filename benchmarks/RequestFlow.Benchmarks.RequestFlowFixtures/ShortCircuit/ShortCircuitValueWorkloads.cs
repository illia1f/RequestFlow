using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.RequestFlowFixtures.ShortCircuit;

public sealed class ShortCircuitValueRequest(ShortCircuitOutcome outcome)
    : ShortCircuitMessage(outcome), IValueRequest<int>;

public sealed class ShortCircuitValueAuthorization(DependencyState state, AccessPolicy dependency)
    : DependencyComponent(state, "authorization"), IValueRequestStage<ShortCircuitValueRequest, int>
{
    public ValueTask<int> HandleAsync(
        ShortCircuitValueRequest request,
        ValueContinuation<int> next,
        CancellationToken token)
    {
        Enter(request, token);

        return dependency.Allows(request) ? next.InvokeAsync(token) : new ValueTask<int>(ShortCircuitWorkload.RejectedResult);
    }
}

public sealed class ShortCircuitValueCache(DependencyState state, CachePolicy dependency)
    : DependencyComponent(state, "cache"), IValueRequestStage<ShortCircuitValueRequest, int>
{
    public ValueTask<int> HandleAsync(
        ShortCircuitValueRequest request,
        ValueContinuation<int> next,
        CancellationToken token)
    {
        Enter(request, token);

        return dependency.Contains(request) ? new ValueTask<int>(ShortCircuitWorkload.CachedResult) : next.InvokeAsync(token);
    }
}

public sealed class ShortCircuitValueTransaction(DependencyState state, UnitOfWork dependency)
    : DependencyComponent(state, "transaction"), IValueRequestStage<ShortCircuitValueRequest, int>
{
    public ValueTask<int> HandleAsync(
        ShortCircuitValueRequest request,
        ValueContinuation<int> next,
        CancellationToken token)
    {
        Enter(request, token);

        return dependency.Capacity >= 0 ? next.InvokeAsync(token) : throw new InvalidOperationException();
    }
}

public sealed class ShortCircuitValueHandler(DependencyState state, Repository dependency)
    : DependencyComponent(state, TraceNames.Handler), IValueRequestHandler<ShortCircuitValueRequest, int>
{
    public ValueTask<int> HandleAsync(ShortCircuitValueRequest request, CancellationToken token)
    {
        Enter(request, token);

        ValueTask<int> result = dependency.ReadValue();

        return request.Trace is { } trace ? trace.Track(result) : result;
    }
}
