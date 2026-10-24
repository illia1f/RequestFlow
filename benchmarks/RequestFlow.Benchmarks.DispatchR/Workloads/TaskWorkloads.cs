using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.DispatchR;

public sealed class DispatchRTaskHandler(ScopeWitness scope)
    : WorkloadComponent(scope), global::DispatchR.Abstractions.Send.IRequestHandler<DispatchRTaskRequest, Task<int>>
{
    public Task<int> Handle(DispatchRTaskRequest request, CancellationToken cancellationToken)
    {
        Observe(request, TraceNames.Handler, cancellationToken);

        return BenchmarkWorkload.TaskResponse(request, cancellationToken);
    }
}

public abstract class DispatchRTaskStage<TSlot>(ScopeWitness scope) : WorkloadComponent(scope) where TSlot : IStageSlot
{
    public required global::DispatchR.Abstractions.Send.IRequestHandler<DispatchRTaskRequest, Task<int>> NextPipeline { get; set; }

    public Task<int> Handle(DispatchRTaskRequest request, CancellationToken cancellationToken)
    {
        Observe(request, StageSlot<TSlot>.StageName, cancellationToken);

        Task<int> result = NextPipeline.Handle(request, cancellationToken);

        return request.Completion == CompletionKind.Suspending ? BenchmarkWorkload.AwaitTask(result) : result;
    }
}

public sealed class DispatchRTaskStage1(ScopeWitness scope)
    : DispatchRTaskStage<Slot1>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRTaskRequest, Task<int>>;

public sealed class DispatchRTaskStage2(ScopeWitness scope)
    : DispatchRTaskStage<Slot2>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRTaskRequest, Task<int>>;

public sealed class DispatchRTaskStage3(ScopeWitness scope)
    : DispatchRTaskStage<Slot3>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRTaskRequest, Task<int>>;

public sealed class DispatchRTaskStage4(ScopeWitness scope)
    : DispatchRTaskStage<Slot4>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRTaskRequest, Task<int>>;

public sealed class DispatchRTaskStage5(ScopeWitness scope)
    : DispatchRTaskStage<Slot5>(scope), global::DispatchR.Abstractions.Send.IPipelineBehavior<DispatchRTaskRequest, Task<int>>;
