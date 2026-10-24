using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.DispatchR;

public sealed class DispatchRStreamHandler(ScopeWitness scope)
    : WorkloadComponent(scope), global::DispatchR.Abstractions.Stream.IStreamRequestHandler<DispatchRStreamRequest, int>
{
    public IAsyncEnumerable<int> Handle(DispatchRStreamRequest request, CancellationToken cancellationToken)
    {
        Observe(request, TraceNames.Handler, cancellationToken);

        return RuntimeTracing.Stream(request, request.Count, this, cancellationToken);
    }
}

public abstract class DispatchRStreamStage<TSlot>(ScopeWitness scope) : WorkloadComponent(scope) where TSlot : IStageSlot
{
    public required global::DispatchR.Abstractions.Stream.IStreamRequestHandler<DispatchRStreamRequest, int> NextPipeline { get; set; }

    public IAsyncEnumerable<int> Handle(DispatchRStreamRequest request, CancellationToken cancellationToken)
    {
        Observe(request, StageSlot<TSlot>.StageName, cancellationToken);

        return NextPipeline.Handle(request, cancellationToken);
    }
}

public sealed class DispatchRStreamStage1(ScopeWitness scope)
    : DispatchRStreamStage<Slot1>(scope), global::DispatchR.Abstractions.Stream.IStreamPipelineBehavior<DispatchRStreamRequest, int>;

public sealed class DispatchRStreamStage2(ScopeWitness scope)
    : DispatchRStreamStage<Slot2>(scope), global::DispatchR.Abstractions.Stream.IStreamPipelineBehavior<DispatchRStreamRequest, int>;

public sealed class DispatchRStreamStage3(ScopeWitness scope)
    : DispatchRStreamStage<Slot3>(scope), global::DispatchR.Abstractions.Stream.IStreamPipelineBehavior<DispatchRStreamRequest, int>;

public sealed class DispatchRStreamStage4(ScopeWitness scope)
    : DispatchRStreamStage<Slot4>(scope), global::DispatchR.Abstractions.Stream.IStreamPipelineBehavior<DispatchRStreamRequest, int>;

public sealed class DispatchRStreamStage5(ScopeWitness scope)
    : DispatchRStreamStage<Slot5>(scope), global::DispatchR.Abstractions.Stream.IStreamPipelineBehavior<DispatchRStreamRequest, int>;
