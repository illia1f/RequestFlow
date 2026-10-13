using MediatR;
using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.MediatR;

public sealed class MediatREventHandler1(ScopeWitness scope) : MediatREventHandler<Slot1>(scope);
public sealed class MediatREventHandler2(ScopeWitness scope) : MediatREventHandler<Slot2>(scope);
public sealed class MediatREventHandler3(ScopeWitness scope) : MediatREventHandler<Slot3>(scope);
public sealed class MediatREventHandler4(ScopeWitness scope) : MediatREventHandler<Slot4>(scope);
public sealed class MediatREventHandler5(ScopeWitness scope) : MediatREventHandler<Slot5>(scope);

public abstract class MediatREventHandler<TSlot>(ScopeWitness scope)
    : WorkloadComponent(scope), INotificationHandler<MediatREvent>
    where TSlot : IStageSlot
{
    public Task Handle(MediatREvent notification, CancellationToken cancellationToken)
    {
        Observe(notification, StageSlot<TSlot>.HandlerName, cancellationToken);

        return RuntimeTracing.EventTask(notification, this, StageSlot<TSlot>.HandlerName, cancellationToken);
    }
}
