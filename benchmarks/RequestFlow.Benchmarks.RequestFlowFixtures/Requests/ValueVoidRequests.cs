using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.RequestFlowFixtures;

public sealed class ValueVoidRequest(CompletionKind completion = CompletionKind.Cached)
    : WorkloadMessage(completion), IValueRequest;

public sealed class ValueVoidRequestHandler(ScopeWitness scope) : WorkloadComponent(scope), IValueRequestHandler<ValueVoidRequest>
{
    public ValueTask HandleAsync(ValueVoidRequest request, CancellationToken cancellationToken)
    {
        Observe(request, TraceNames.Handler, cancellationToken);

        return BenchmarkWorkload.ValueVoidRequest(request, cancellationToken);
    }
}

public abstract class ValueVoidRequestStage<TSlot>(ScopeWitness scope)
    : WorkloadComponent(scope), IValueRequestStage<ValueVoidRequest>
    where TSlot : IStageSlot
{
    public ValueTask HandleAsync(ValueVoidRequest request, ValueContinuation next, CancellationToken cancellationToken)
    {
        Observe(request, StageSlot<TSlot>.StageName, cancellationToken);

        ValueTask result = next.InvokeAsync(cancellationToken);

        return request.Completion == CompletionKind.Suspending ? BenchmarkWorkload.AwaitValue(result) : result;
    }
}

public sealed class ValueVoidRequestStage1(ScopeWitness scope) : ValueVoidRequestStage<Slot1>(scope);
public sealed class ValueVoidRequestStage2(ScopeWitness scope) : ValueVoidRequestStage<Slot2>(scope);
public sealed class ValueVoidRequestStage3(ScopeWitness scope) : ValueVoidRequestStage<Slot3>(scope);
public sealed class ValueVoidRequestStage4(ScopeWitness scope) : ValueVoidRequestStage<Slot4>(scope);
public sealed class ValueVoidRequestStage5(ScopeWitness scope) : ValueVoidRequestStage<Slot5>(scope);
