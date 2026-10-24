using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.DispatchR;

public abstract class DispatchREventHandler<TSlot>(ScopeWitness scope)
    : WorkloadComponent(scope), global::DispatchR.Abstractions.Notification.INotificationHandler<DispatchREvent> where TSlot : IStageSlot
{
    public ValueTask Handle(DispatchREvent request, CancellationToken cancellationToken)
    {
        Observe(request, StageSlot<TSlot>.HandlerName, cancellationToken);

        return RuntimeTracing.EventValue(request, this, StageSlot<TSlot>.HandlerName, cancellationToken);
    }
}

public sealed class DispatchREventHandler1(ScopeWitness scope) : DispatchREventHandler<Slot1>(scope);

public sealed class DispatchREventHandler2(ScopeWitness scope) : DispatchREventHandler<Slot2>(scope);

public sealed class DispatchREventHandler3(ScopeWitness scope) : DispatchREventHandler<Slot3>(scope);

public sealed class DispatchREventHandler4(ScopeWitness scope) : DispatchREventHandler<Slot4>(scope);

public sealed class DispatchREventHandler5(ScopeWitness scope) : DispatchREventHandler<Slot5>(scope);
