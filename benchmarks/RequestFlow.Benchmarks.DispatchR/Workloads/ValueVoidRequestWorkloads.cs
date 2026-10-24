using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.DispatchR;

public sealed class DispatchRValueVoidRequestHandler(ScopeWitness scope)
    : WorkloadComponent(scope), global::DispatchR.Abstractions.Send.IRequestHandler<DispatchRValueVoidRequest, ValueTask<DispatchRVoidRequestResult>>
{
    public ValueTask<DispatchRVoidRequestResult> Handle(DispatchRValueVoidRequest request, CancellationToken cancellationToken)
    {
        Observe(request, TraceNames.Handler, cancellationToken);

        return BenchmarkWorkload.ValueUnitVoidRequest<DispatchRVoidRequestResult>(request, cancellationToken);
    }
}

public abstract class DispatchRValueVoidRequestStage<TSlot>(ScopeWitness scope) : WorkloadComponent(scope) where TSlot : IStageSlot
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DispatchRValueVoidRequest, ValueTask<DispatchRVoidRequestResult>> NextPipeline { get; set; }

    public ValueTask<DispatchRVoidRequestResult> Handle(DispatchRValueVoidRequest request, CancellationToken cancellationToken)
    {
        Observe(request, StageSlot<TSlot>.StageName, cancellationToken);

        ValueTask<DispatchRVoidRequestResult> result = NextPipeline.Handle(request, cancellationToken);

        return request.Completion == CompletionKind.Suspending ? BenchmarkWorkload.AwaitValue(result) : result;
    }
}

public sealed class DispatchRValueVoidRequestStage1(ScopeWitness scope)
    : DispatchRValueVoidRequestStage<Slot1>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRValueVoidRequest, ValueTask<DispatchRVoidRequestResult>>;

public sealed class DispatchRValueVoidRequestStage2(ScopeWitness scope)
    : DispatchRValueVoidRequestStage<Slot2>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRValueVoidRequest, ValueTask<DispatchRVoidRequestResult>>;

public sealed class DispatchRValueVoidRequestStage3(ScopeWitness scope)
    : DispatchRValueVoidRequestStage<Slot3>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRValueVoidRequest, ValueTask<DispatchRVoidRequestResult>>;

public sealed class DispatchRValueVoidRequestStage4(ScopeWitness scope)
    : DispatchRValueVoidRequestStage<Slot4>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRValueVoidRequest, ValueTask<DispatchRVoidRequestResult>>;

public sealed class DispatchRValueVoidRequestStage5(ScopeWitness scope)
    : DispatchRValueVoidRequestStage<Slot5>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRValueVoidRequest, ValueTask<DispatchRVoidRequestResult>>;
