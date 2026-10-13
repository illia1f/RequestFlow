using MediatR;
using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.MediatR;

public sealed class MediatRStreamHandler(ScopeWitness scope)
    : WorkloadComponent(scope), global::MediatR.IStreamRequestHandler<MediatRStreamRequest, int>
{
    public IAsyncEnumerable<int> Handle(MediatRStreamRequest request, CancellationToken cancellationToken)
    {
        Observe(request, TraceNames.Handler, cancellationToken);

        return RuntimeTracing.Stream(request, request.Count, this, cancellationToken);
    }
}

public sealed class MediatRStreamStage1(ScopeWitness scope) : MediatRStreamStage<Slot1>(scope);
public sealed class MediatRStreamStage2(ScopeWitness scope) : MediatRStreamStage<Slot2>(scope);
public sealed class MediatRStreamStage3(ScopeWitness scope) : MediatRStreamStage<Slot3>(scope);
public sealed class MediatRStreamStage4(ScopeWitness scope) : MediatRStreamStage<Slot4>(scope);
public sealed class MediatRStreamStage5(ScopeWitness scope) : MediatRStreamStage<Slot5>(scope);

public abstract class MediatRStreamStage<TSlot>(ScopeWitness scope)
    : WorkloadComponent(scope), IStreamPipelineBehavior<MediatRStreamRequest, int>
    where TSlot : IStageSlot
{
    public IAsyncEnumerable<int> Handle(
        MediatRStreamRequest request,
        StreamHandlerDelegate<int> next,
        CancellationToken cancellationToken)
    {
        Observe(request, StageSlot<TSlot>.StageName, cancellationToken);

        return next();
    }
}
