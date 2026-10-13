using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.RequestFlowFixtures;

public sealed class StreamRequest(int count = 10, CompletionKind completion = CompletionKind.Synchronous)
    : WorkloadMessage(completion), IStreamRequest<int>
{
    public int Count { get; } = count;
}

public sealed class StreamHandler(ScopeWitness scope)
    : WorkloadComponent(scope), IStreamRequestHandler<StreamRequest, int>
{
    public IAsyncEnumerable<int> Handle(StreamRequest request, CancellationToken cancellationToken)
    {
        Observe(request, TraceNames.Handler, cancellationToken);

        return RuntimeTracing.Stream(request, request.Count, this, cancellationToken);
    }
}

public abstract class StreamStage<TSlot>(ScopeWitness scope)
    : WorkloadComponent(scope), IStreamRequestStage<StreamRequest, int>
    where TSlot : IStageSlot
{
    public IAsyncEnumerable<int> Handle(
        StreamRequest request,
        StreamContinuation<int> next,
        CancellationToken cancellationToken)
    {
        Observe(request, StageSlot<TSlot>.StageName, cancellationToken);

        return next.Invoke(cancellationToken);
    }
}

public sealed class StreamStage1(ScopeWitness scope) : StreamStage<Slot1>(scope);
public sealed class StreamStage2(ScopeWitness scope) : StreamStage<Slot2>(scope);
public sealed class StreamStage3(ScopeWitness scope) : StreamStage<Slot3>(scope);
public sealed class StreamStage4(ScopeWitness scope) : StreamStage<Slot4>(scope);
public sealed class StreamStage5(ScopeWitness scope) : StreamStage<Slot5>(scope);
