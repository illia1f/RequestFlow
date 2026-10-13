using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.RequestFlowFixtures;

public sealed class EventMessage(CompletionKind completion = CompletionKind.Cached)
    : WorkloadMessage(completion), IEvent;

public abstract class EventMessageHandler<TSlot>(ScopeWitness scope) : WorkloadComponent(scope), IEventHandler<EventMessage>
    where TSlot : IStageSlot
{
    public Task HandleAsync(EventMessage @event, CancellationToken cancellationToken)
    {
        Observe(@event, StageSlot<TSlot>.HandlerName, cancellationToken);

        return RuntimeTracing.EventTask(@event, this, StageSlot<TSlot>.HandlerName, cancellationToken);
    }
}

public sealed class EventMessageHandler1(ScopeWitness scope) : EventMessageHandler<Slot1>(scope);
public sealed class EventMessageHandler2(ScopeWitness scope) : EventMessageHandler<Slot2>(scope);
public sealed class EventMessageHandler3(ScopeWitness scope) : EventMessageHandler<Slot3>(scope);
public sealed class EventMessageHandler4(ScopeWitness scope) : EventMessageHandler<Slot4>(scope);
public sealed class EventMessageHandler5(ScopeWitness scope) : EventMessageHandler<Slot5>(scope);
