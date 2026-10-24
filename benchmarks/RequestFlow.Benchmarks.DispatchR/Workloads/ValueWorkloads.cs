using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.DispatchR;

public sealed class DispatchRValueHandler(ScopeWitness scope)
    : WorkloadComponent(scope), global::DispatchR.Abstractions.Send.IRequestHandler<DispatchRValueRequest, ValueTask<int>>
{
    public ValueTask<int> Handle(DispatchRValueRequest request, CancellationToken cancellationToken)
    {
        Observe(request, TraceNames.Handler, cancellationToken);

        return BenchmarkWorkload.ValueResponse(request, cancellationToken);
    }
}

public abstract class DispatchRValueStage<TSlot>(ScopeWitness scope) : WorkloadComponent(scope) where TSlot : IStageSlot
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DispatchRValueRequest, ValueTask<int>> NextPipeline { get; set; }

    public ValueTask<int> Handle(DispatchRValueRequest request, CancellationToken cancellationToken)
    {
        Observe(request, StageSlot<TSlot>.StageName, cancellationToken);

        ValueTask<int> result = NextPipeline.Handle(request, cancellationToken);

        return request.Completion == CompletionKind.Suspending ? BenchmarkWorkload.AwaitValue(result) : result;
    }
}

public sealed class DispatchRValueStage1(ScopeWitness scope)
    : DispatchRValueStage<Slot1>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRValueRequest, ValueTask<int>>;

public sealed class DispatchRValueStage2(ScopeWitness scope)
    : DispatchRValueStage<Slot2>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRValueRequest, ValueTask<int>>;

public sealed class DispatchRValueStage3(ScopeWitness scope)
    : DispatchRValueStage<Slot3>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRValueRequest, ValueTask<int>>;

public sealed class DispatchRValueStage4(ScopeWitness scope)
    : DispatchRValueStage<Slot4>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRValueRequest, ValueTask<int>>;

public sealed class DispatchRValueStage5(ScopeWitness scope)
    : DispatchRValueStage<Slot5>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRValueRequest, ValueTask<int>>;
