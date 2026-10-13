using MediatR;
using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.MediatR;

public sealed class MediatRTaskHandler(ScopeWitness scope)
    : WorkloadComponent(scope), global::MediatR.IRequestHandler<MediatRTaskRequest, int>
{
    public Task<int> Handle(MediatRTaskRequest request, CancellationToken cancellationToken)
    {
        Observe(request, TraceNames.Handler, cancellationToken);

        return BenchmarkWorkload.TaskResponse(request, cancellationToken);
    }
}

public sealed class MediatRTaskStage1(ScopeWitness scope) : MediatRTaskStage<Slot1>(scope);
public sealed class MediatRTaskStage2(ScopeWitness scope) : MediatRTaskStage<Slot2>(scope);
public sealed class MediatRTaskStage3(ScopeWitness scope) : MediatRTaskStage<Slot3>(scope);
public sealed class MediatRTaskStage4(ScopeWitness scope) : MediatRTaskStage<Slot4>(scope);
public sealed class MediatRTaskStage5(ScopeWitness scope) : MediatRTaskStage<Slot5>(scope);

public abstract class MediatRTaskStage<TSlot>(ScopeWitness scope)
    : WorkloadComponent(scope), IPipelineBehavior<MediatRTaskRequest, int>
    where TSlot : IStageSlot
{
    public Task<int> Handle(
        MediatRTaskRequest request,
        RequestHandlerDelegate<int> next,
        CancellationToken cancellationToken)
    {
        Observe(request, StageSlot<TSlot>.StageName, cancellationToken);

        Task<int> result = next(cancellationToken);

        return request.Completion == CompletionKind.Suspending ? BenchmarkWorkload.AwaitTask(result) : result;
    }
}
