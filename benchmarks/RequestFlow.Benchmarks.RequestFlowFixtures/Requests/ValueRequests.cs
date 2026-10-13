using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.RequestFlowFixtures;

public sealed class ValueRequest(CompletionKind completion = CompletionKind.Cached, int value = 42)
    : WorkloadMessage(completion, value), IValueRequest<int>;

public sealed class ValueRequestHandler(ScopeWitness scope)
    : WorkloadComponent(scope), IValueRequestHandler<ValueRequest, int>
{
    public ValueTask<int> HandleAsync(ValueRequest request, CancellationToken cancellationToken)
    {
        Observe(request, TraceNames.Handler, cancellationToken);

        return BenchmarkWorkload.ValueResponse(request, cancellationToken);
    }
}

public abstract class ValueStage<TSlot>(ScopeWitness scope)
    : WorkloadComponent(scope), IValueRequestStage<ValueRequest, int>
    where TSlot : IStageSlot
{
    public ValueTask<int> HandleAsync(
        ValueRequest request,
        ValueContinuation<int> next,
        CancellationToken cancellationToken)
    {
        Observe(request, StageSlot<TSlot>.StageName, cancellationToken);

        ValueTask<int> result = next.InvokeAsync(cancellationToken);

        return request.Completion == CompletionKind.Suspending ? BenchmarkWorkload.AwaitValue(result) : result;
    }
}

public sealed class ValueStage1(ScopeWitness scope) : ValueStage<Slot1>(scope);
public sealed class ValueStage2(ScopeWitness scope) : ValueStage<Slot2>(scope);
public sealed class ValueStage3(ScopeWitness scope) : ValueStage<Slot3>(scope);
public sealed class ValueStage4(ScopeWitness scope) : ValueStage<Slot4>(scope);
public sealed class ValueStage5(ScopeWitness scope) : ValueStage<Slot5>(scope);
