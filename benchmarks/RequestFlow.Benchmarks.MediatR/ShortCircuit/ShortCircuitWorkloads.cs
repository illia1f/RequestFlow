using MediatR;
using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.MediatR;

public sealed class MediatRShortCircuitRequest(ShortCircuitOutcome outcome)
    : ShortCircuitMessage(outcome), global::MediatR.IRequest<int>;

public sealed class MediatRShortCircuitAuthorization(DependencyState state, AccessPolicy dependency)
    : DependencyComponent(state, "authorization"), IPipelineBehavior<MediatRShortCircuitRequest, int>
{
    public Task<int> Handle(
        MediatRShortCircuitRequest request,
        RequestHandlerDelegate<int> next,
        CancellationToken token)
    {
        Enter(request, token);

        return dependency.Allows(request) ? next(token) : ShortCircuitWorkload.Rejected;
    }
}

public sealed class MediatRShortCircuitCache(DependencyState state, CachePolicy dependency)
    : DependencyComponent(state, "cache"), IPipelineBehavior<MediatRShortCircuitRequest, int>
{
    public Task<int> Handle(
        MediatRShortCircuitRequest request,
        RequestHandlerDelegate<int> next,
        CancellationToken token)
    {
        Enter(request, token);

        return dependency.Contains(request) ? ShortCircuitWorkload.Cached : next(token);
    }
}

public sealed class MediatRShortCircuitTransaction(DependencyState state, UnitOfWork dependency)
    : DependencyComponent(state, "transaction"), IPipelineBehavior<MediatRShortCircuitRequest, int>
{
    public Task<int> Handle(
        MediatRShortCircuitRequest request,
        RequestHandlerDelegate<int> next,
        CancellationToken token)
    {
        Enter(request, token);

        return dependency.Capacity >= 0 ? next(token) : throw new InvalidOperationException();
    }
}

public sealed class MediatRShortCircuitHandler(DependencyState state, Repository dependency)
    : DependencyComponent(state, TraceNames.Handler), global::MediatR.IRequestHandler<MediatRShortCircuitRequest, int>
{
    public Task<int> Handle(MediatRShortCircuitRequest request, CancellationToken token)
    {
        Enter(request, token);

        Task<int> result = dependency.Read();

        return request.Trace is { } trace ? trace.Track(result) : result;
    }
}
