using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.DispatchR;

public sealed class DispatchRCatalogRequest<TTag>(CompletionKind completion = CompletionKind.Cached)
    : WorkloadMessage(completion), global::DispatchR.Abstractions.Send.IRequest<DispatchRCatalogRequest<TTag>, Task<int>>;

public sealed class DispatchRCatalogHandler<TTag>(ScopeWitness scope)
    : WorkloadComponent(scope), global::DispatchR.Abstractions.Send.IRequestHandler<DispatchRCatalogRequest<TTag>, Task<int>>
{
    public Task<int> Handle(DispatchRCatalogRequest<TTag> request, CancellationToken cancellationToken)
    {
        Observe(request, TraceNames.Handler, cancellationToken);

        return BenchmarkWorkload.TaskResponse(request, cancellationToken);
    }
}
