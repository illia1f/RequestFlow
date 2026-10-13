using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.RequestFlowFixtures.ShortCircuit;

public sealed class ShortCircuitTaskRequest(ShortCircuitOutcome outcome)
    : ShortCircuitMessage(outcome), IRequest<int>;

public sealed class ShortCircuitTaskAuthorization(DependencyState state, AccessPolicy dependency)
    : DependencyComponent(state, "authorization"), IRequestStage<ShortCircuitTaskRequest, int>
{
    public Task<int> HandleAsync(
        ShortCircuitTaskRequest request,
        Continuation<int> next,
        CancellationToken token)
    {
        Enter(request, token);

        return dependency.Allows(request) ? next.InvokeAsync(token) : ShortCircuitWorkload.Rejected;
    }
}

public sealed class ShortCircuitTaskCache(DependencyState state, CachePolicy dependency)
    : DependencyComponent(state, "cache"), IRequestStage<ShortCircuitTaskRequest, int>
{
    public Task<int> HandleAsync(
        ShortCircuitTaskRequest request,
        Continuation<int> next,
        CancellationToken token)
    {
        Enter(request, token);

        return dependency.Contains(request) ? ShortCircuitWorkload.Cached : next.InvokeAsync(token);
    }
}

public sealed class ShortCircuitTaskTransaction(DependencyState state, UnitOfWork dependency)
    : DependencyComponent(state, "transaction"), IRequestStage<ShortCircuitTaskRequest, int>
{
    public Task<int> HandleAsync(
        ShortCircuitTaskRequest request,
        Continuation<int> next,
        CancellationToken token)
    {
        Enter(request, token);

        return dependency.Capacity >= 0 ? next.InvokeAsync(token) : throw new InvalidOperationException();
    }
}

public sealed class ShortCircuitTaskHandler(DependencyState state, Repository dependency)
    : DependencyComponent(state, TraceNames.Handler), IRequestHandler<ShortCircuitTaskRequest, int>
{
    public Task<int> HandleAsync(ShortCircuitTaskRequest request, CancellationToken token)
    {
        Enter(request, token);

        Task<int> result = dependency.Read();

        return request.Trace is { } trace ? trace.Track(result) : result;
    }
}
