using MediatR;
using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.MediatR;

public sealed class MediatRVoidRequestHandler(ScopeWitness scope)
    : WorkloadComponent(scope), global::MediatR.IRequestHandler<MediatRVoidRequest>
{
    public Task Handle(MediatRVoidRequest request, CancellationToken cancellationToken)
    {
        Observe(request, TraceNames.Handler, cancellationToken);

        return BenchmarkWorkload.TaskVoidRequest(request, cancellationToken);
    }
}

public sealed class MediatRVoidRequestStage1(ScopeWitness scope) : MediatRVoidRequestStage<Slot1>(scope);
public sealed class MediatRVoidRequestStage2(ScopeWitness scope) : MediatRVoidRequestStage<Slot2>(scope);
public sealed class MediatRVoidRequestStage3(ScopeWitness scope) : MediatRVoidRequestStage<Slot3>(scope);
public sealed class MediatRVoidRequestStage4(ScopeWitness scope) : MediatRVoidRequestStage<Slot4>(scope);
public sealed class MediatRVoidRequestStage5(ScopeWitness scope) : MediatRVoidRequestStage<Slot5>(scope);

public abstract class MediatRVoidRequestStage<TSlot>(ScopeWitness scope)
    : WorkloadComponent(scope), IPipelineBehavior<MediatRVoidRequest, Unit>
    where TSlot : IStageSlot
{
    public Task<Unit> Handle(
        MediatRVoidRequest request,
        RequestHandlerDelegate<Unit> next,
        CancellationToken cancellationToken)
    {
        Observe(request, StageSlot<TSlot>.StageName, cancellationToken);

        Task<Unit> result = next(cancellationToken);

        return request.Completion == CompletionKind.Suspending ? BenchmarkWorkload.AwaitTask(result) : result;
    }
}
