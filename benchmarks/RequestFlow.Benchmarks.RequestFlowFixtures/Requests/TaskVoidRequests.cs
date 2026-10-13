using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.RequestFlowFixtures;

public sealed class TaskVoidRequest(CompletionKind completion = CompletionKind.Cached)
    : WorkloadMessage(completion), IRequest;

public sealed class TaskVoidRequestHandler(ScopeWitness scope) : WorkloadComponent(scope), IRequestHandler<TaskVoidRequest>
{
    public Task HandleAsync(TaskVoidRequest request, CancellationToken cancellationToken)
    {
        Observe(request, TraceNames.Handler, cancellationToken);

        return BenchmarkWorkload.TaskVoidRequest(request, cancellationToken);
    }
}

public abstract class TaskVoidRequestStage<TSlot>(ScopeWitness scope) : WorkloadComponent(scope), IRequestStage<TaskVoidRequest>
    where TSlot : IStageSlot
{
    public Task HandleAsync(TaskVoidRequest request, Continuation next, CancellationToken cancellationToken)
    {
        Observe(request, StageSlot<TSlot>.StageName, cancellationToken);

        Task result = next.InvokeAsync(cancellationToken);

        return request.Completion == CompletionKind.Suspending ? BenchmarkWorkload.AwaitTask(result) : result;
    }
}

public sealed class TaskVoidRequestStage1(ScopeWitness scope) : TaskVoidRequestStage<Slot1>(scope);
public sealed class TaskVoidRequestStage2(ScopeWitness scope) : TaskVoidRequestStage<Slot2>(scope);
public sealed class TaskVoidRequestStage3(ScopeWitness scope) : TaskVoidRequestStage<Slot3>(scope);
public sealed class TaskVoidRequestStage4(ScopeWitness scope) : TaskVoidRequestStage<Slot4>(scope);
public sealed class TaskVoidRequestStage5(ScopeWitness scope) : TaskVoidRequestStage<Slot5>(scope);
