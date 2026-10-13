using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.MediatR;

public sealed class MediatRCatalogRequest<TTag>(CompletionKind completion = CompletionKind.Cached)
    : WorkloadMessage(completion), global::MediatR.IRequest<int>;

public sealed class MediatRCatalogHandler<TTag>(ScopeWitness scope)
    : WorkloadComponent(scope), global::MediatR.IRequestHandler<MediatRCatalogRequest<TTag>, int>
{
    public Task<int> Handle(MediatRCatalogRequest<TTag> request, CancellationToken cancellationToken)
    {
        Observe(request, TraceNames.Handler, cancellationToken);

        return BenchmarkWorkload.TaskResponse(request, cancellationToken);
    }
}
