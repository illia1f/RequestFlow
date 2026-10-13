using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.RequestFlowFixtures.Catalog;

public sealed class RuntimeTaskRequest<TTag>(CompletionKind completion = CompletionKind.Cached)
    : WorkloadMessage(completion), IRequest<int>;

public sealed class RuntimeTaskHandler<TTag>(ScopeWitness scope)
    : WorkloadComponent(scope), IRequestHandler<RuntimeTaskRequest<TTag>, int>
{
    public Task<int> HandleAsync(RuntimeTaskRequest<TTag> request, CancellationToken cancellationToken)
    {
        Observe(request, TraceNames.Handler, cancellationToken);

        return BenchmarkWorkload.TaskResponse(request, cancellationToken);
    }
}
