using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.DispatchR;

public sealed class DispatchRVoidRequestHandler(ScopeWitness scope)
    : WorkloadComponent(scope), global::DispatchR.Abstractions.Send.IRequestHandler<DispatchRVoidRequest, Task<DispatchRVoidRequestResult>>
{
    public Task<DispatchRVoidRequestResult> Handle(DispatchRVoidRequest request, CancellationToken cancellationToken)
    {
        Observe(request, TraceNames.Handler, cancellationToken);

        return BenchmarkWorkload.TaskUnitVoidRequest<DispatchRVoidRequestResult>(request, cancellationToken);
    }
}

public abstract class DispatchRVoidRequestStage<TSlot>(ScopeWitness scope) : WorkloadComponent(scope) where TSlot : IStageSlot
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DispatchRVoidRequest, Task<DispatchRVoidRequestResult>> NextPipeline { get; set; }

    public Task<DispatchRVoidRequestResult> Handle(DispatchRVoidRequest request, CancellationToken cancellationToken)
    {
        Observe(request, StageSlot<TSlot>.StageName, cancellationToken);

        Task<DispatchRVoidRequestResult> result = NextPipeline.Handle(request, cancellationToken);

        return request.Completion == CompletionKind.Suspending ? BenchmarkWorkload.AwaitTask(result) : result;
    }
}

public sealed class DispatchRVoidRequestStage1(ScopeWitness scope)
    : DispatchRVoidRequestStage<Slot1>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRVoidRequest, Task<DispatchRVoidRequestResult>>;

public sealed class DispatchRVoidRequestStage2(ScopeWitness scope)
    : DispatchRVoidRequestStage<Slot2>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRVoidRequest, Task<DispatchRVoidRequestResult>>;

public sealed class DispatchRVoidRequestStage3(ScopeWitness scope)
    : DispatchRVoidRequestStage<Slot3>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRVoidRequest, Task<DispatchRVoidRequestResult>>;

public sealed class DispatchRVoidRequestStage4(ScopeWitness scope)
    : DispatchRVoidRequestStage<Slot4>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRVoidRequest, Task<DispatchRVoidRequestResult>>;

public sealed class DispatchRVoidRequestStage5(ScopeWitness scope)
    : DispatchRVoidRequestStage<Slot5>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRVoidRequest, Task<DispatchRVoidRequestResult>>;
