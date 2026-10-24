using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.DispatchR;

public sealed class DispatchRShortCircuitRequest(ShortCircuitOutcome outcome)
    : ShortCircuitMessage(outcome), global::DispatchR.Abstractions.Send.IRequest<DispatchRShortCircuitRequest, Task<int>>;

public sealed class DispatchRShortCircuitAuthorization(DependencyState state, AccessPolicy dependency)
    : DependencyComponent(state, "authorization"), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRShortCircuitRequest, Task<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DispatchRShortCircuitRequest, Task<int>> NextPipeline { get; set; }

    public Task<int> Handle(DispatchRShortCircuitRequest request, CancellationToken token)
    {
        Enter(request, token);

        return dependency.Allows(request) ? NextPipeline.Handle(request, token) : ShortCircuitWorkload.Rejected;
    }
}

public sealed class DispatchRShortCircuitCache(DependencyState state, CachePolicy dependency)
    : DependencyComponent(state, "cache"), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRShortCircuitRequest, Task<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DispatchRShortCircuitRequest, Task<int>> NextPipeline { get; set; }

    public Task<int> Handle(DispatchRShortCircuitRequest request, CancellationToken token)
    {
        Enter(request, token);

        return dependency.Contains(request) ? ShortCircuitWorkload.Cached : NextPipeline.Handle(request, token);
    }
}

public sealed class DispatchRShortCircuitTransaction(DependencyState state, UnitOfWork dependency)
    : DependencyComponent(state, "transaction"), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRShortCircuitRequest, Task<int>>
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DispatchRShortCircuitRequest, Task<int>> NextPipeline { get; set; }

    public Task<int> Handle(DispatchRShortCircuitRequest request, CancellationToken token)
    {
        Enter(request, token);

        return dependency.Capacity >= 0 ? NextPipeline.Handle(request, token) : throw new InvalidOperationException();
    }
}

public sealed class DispatchRShortCircuitHandler(DependencyState state, Repository dependency)
    : DependencyComponent(state, TraceNames.Handler), global::DispatchR.Abstractions.Send.IRequestHandler<DispatchRShortCircuitRequest, Task<int>>
{
    public Task<int> Handle(DispatchRShortCircuitRequest request, CancellationToken token)
    {
        Enter(request, token);

        Task<int> result = dependency.Read();

        return request.Trace is { } trace ? trace.Track(result) : result;
    }
}
