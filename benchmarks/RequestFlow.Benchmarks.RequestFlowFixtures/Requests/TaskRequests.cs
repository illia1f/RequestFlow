using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.RequestFlowFixtures;

public sealed class TaskRequest(CompletionKind completion = CompletionKind.Cached, int value = 42)
    : WorkloadMessage(completion, value), IRequest<int>;

public sealed class TaskRequestHandler(ScopeWitness scope) : WorkloadComponent(scope), IRequestHandler<TaskRequest, int>
{
    public Task<int> HandleAsync(TaskRequest request, CancellationToken cancellationToken)
    {
        Observe(request, TraceNames.Handler, cancellationToken);

        return BenchmarkWorkload.TaskResponse(request, cancellationToken);
    }
}

public abstract class TaskStage<TSlot>(ScopeWitness scope) : WorkloadComponent(scope), IRequestStage<TaskRequest, int>
    where TSlot : IStageSlot
{
    public Task<int> HandleAsync(TaskRequest request, Continuation<int> next, CancellationToken cancellationToken)
    {
        Observe(request, StageSlot<TSlot>.StageName, cancellationToken);

        Task<int> result = next.InvokeAsync(cancellationToken);

        return request.Completion == CompletionKind.Suspending ? BenchmarkWorkload.AwaitTask(result) : result;
    }
}

public sealed class TaskStage1(ScopeWitness scope) : TaskStage<Slot1>(scope);
public sealed class TaskStage2(ScopeWitness scope) : TaskStage<Slot2>(scope);
public sealed class TaskStage3(ScopeWitness scope) : TaskStage<Slot3>(scope);
public sealed class TaskStage4(ScopeWitness scope) : TaskStage<Slot4>(scope);
public sealed class TaskStage5(ScopeWitness scope) : TaskStage<Slot5>(scope);
