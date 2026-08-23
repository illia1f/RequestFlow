using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

internal static class EventPlanFactory
{
    private delegate HandlerEntry HandlerEntryFactory(Type handlerType);

    private static readonly MethodInfo MakeHandlerEntryMethod = typeof(EventPlanFactory)
        .GetMethod(nameof(MakeHandlerEntry), BindingFlags.Static | BindingFlags.NonPublic)!;

    public static EventMap Build(
        EventModel[] events,
        EventStrategyResolution strategyResolution)
    {
        var plans = new Dictionary<Type, EventPlan>();

        // One cache for the whole map, so a subscription reaching many events closes its generic once.
        var factories = new Dictionary<Type, HandlerEntryFactory>();

        foreach (var @event in events)
        {
            Type strategyType = strategyResolution.StrategyFor(@event.EventType)
                ?? throw new InvalidOperationException(
                    $"The event strategy for '{@event.EventType.FullName}' was not resolved.");
            plans[@event.EventType] = PlanFor(
                strategyType,
                @event,
                BuildEntries(@event, factories));
        }

        return new EventMap(plans);
    }

    private static EventPlan PlanFor(
        Type strategyType,
        EventModel @event,
        HandlerEntry[] entries)
    {
        if (strategyType == typeof(SequentialPublishStrategy))
            return new SequentialEventPlan(@event, entries);
        if (strategyType == typeof(ParallelPublishStrategy))
            return new ParallelEventPlan(@event, entries);
        if (strategyType == typeof(FailFastPublishStrategy))
            return new SequentialEventPlan(@event, entries, stopOnFirstFailure: true);

        return new StrategyEventPlan(@event, entries, strategyType);
    }

    private static HandlerEntry[] BuildEntries(
        EventModel @event, Dictionary<Type, HandlerEntryFactory> factories)
    {
        var entries = new HandlerEntry[@event.Handlers.Count];
        for (int i = 0; i < entries.Length; i++)
        {
            EventHandlerModel handler = @event.Handlers[i];
            if (!factories.TryGetValue(handler.DeclaredEventType, out HandlerEntryFactory? factory))
            {
                MethodInfo closed = MakeHandlerEntryMethod.MakeGenericMethod(handler.DeclaredEventType);
                factory = (HandlerEntryFactory)Delegate.CreateDelegate(typeof(HandlerEntryFactory), closed);
                factories.Add(handler.DeclaredEventType, factory);
            }

            entries[i] = factory(handler.HandlerType);
        }

        return entries;
    }

    private static HandlerEntry MakeHandlerEntry<TDeclared>(Type handlerType)
        where TDeclared : IEvent
        => (services, @event, cancellationToken) =>
        {
            var handler = (IEventHandler<TDeclared>)services.GetRequiredService(handlerType);
            return handler.HandleAsync((TDeclared)@event, cancellationToken);
        };
}
