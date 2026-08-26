using System;
using System.Collections.Generic;

namespace RequestFlow;

internal static class EventClosure
{
    internal static EventClosureResult Build(
        IReadOnlyList<Type> knownEvents,
        IReadOnlyList<EventSubscriptionInput> subscriptions)
        => Build(knownEvents, subscriptions, []);

    internal static EventClosureResult Build(
        IReadOnlyList<Type> knownEvents,
        IReadOnlyList<EventSubscriptionInput> subscriptions,
        IReadOnlyList<EventStrategyInput> strategies)
    {
        if (knownEvents is null)
            throw new ArgumentNullException(nameof(knownEvents));
        if (subscriptions is null)
            throw new ArgumentNullException(nameof(subscriptions));
        if (strategies is null)
            throw new ArgumentNullException(nameof(strategies));

        List<Type> eventTypes = BuildEventTypes(knownEvents, subscriptions);
        EventStrategyResolution strategyResolution = ResolveStrategies(eventTypes, strategies);
        var reachedEvents = new List<Type>[subscriptions.Count];
        for (int i = 0; i < reachedEvents.Length; i++)
            reachedEvents[i] = [];

        var events = new EventModel[eventTypes.Count];
        for (int i = 0; i < events.Length; i++)
        {
            Type eventType = eventTypes[i];
            events[i] = new EventModel(
                eventType,
                BuildHandlerModels(eventType, subscriptions, reachedEvents),
                strategyResolution.GetStrategy(eventType));
        }

        return new EventClosureResult(
            events,
            BuildSubscriptionModels(subscriptions, reachedEvents),
            strategyResolution.Models,
            strategyResolution);
    }

    // Fills the reach as it goes: a subscription reaches every event it hands a handler to.
    private static EventHandlerModel[] BuildHandlerModels(
        Type eventType,
        IReadOnlyList<EventSubscriptionInput> subscriptions,
        List<Type>[] reachedEvents)
    {
        var applicable = new List<ApplicableSubscription>();
        for (int subscriptionIndex = 0; subscriptionIndex < subscriptions.Count; subscriptionIndex++)
        {
            EventSubscriptionInput subscription = subscriptions[subscriptionIndex];
            if (subscription.DeclaredEventType.IsAssignableFrom(eventType))
            {
                applicable.Add(new ApplicableSubscription(
                    eventType, subscription, subscriptionIndex));
            }
        }

        applicable.Sort(CompareApplicable);

        var handlers = new EventHandlerModel[applicable.Count];
        for (int handlerIndex = 0; handlerIndex < handlers.Length; handlerIndex++)
        {
            ApplicableSubscription delivery = applicable[handlerIndex];
            EventSubscriptionInput subscription = delivery.Subscription;
            handlers[handlerIndex] = new EventHandlerModel(
                subscription.HandlerType,
                subscription.DeclaredEventType,
                subscription.Lifetime);
            reachedEvents[delivery.SubscriptionIndex].Add(eventType);
        }

        return handlers;
    }

    private static EventSubscriptionModel[] BuildSubscriptionModels(
        IReadOnlyList<EventSubscriptionInput> subscriptions,
        List<Type>[] reachedEvents)
    {
        var models = new EventSubscriptionModel[subscriptions.Count];
        for (int i = 0; i < models.Length; i++)
        {
            EventSubscriptionInput subscription = subscriptions[i];
            models[i] = new EventSubscriptionModel(
                subscription.HandlerType,
                subscription.DeclaredEventType,
                subscription.Lifetime,
                [.. reachedEvents[i]]);
        }

        return models;
    }

    private static EventStrategyResolution ResolveStrategies(
        IReadOnlyList<Type> eventTypes,
        IReadOnlyList<EventStrategyInput> strategies)
    {
        var selected = new Dictionary<Type, Type?>();
        var reachedEvents = new List<Type>[strategies.Count];
        var applicableDeclarations = new bool[strategies.Count];
        var ambiguities = new List<EventStrategyAmbiguity>();
        for (int i = 0; i < reachedEvents.Length; i++)
            reachedEvents[i] = [];

        int globalWinner = FindGlobalWinner(strategies);
        for (int i = 0; i < eventTypes.Count; i++)
        {
            Type eventType = eventTypes[i];
            int winner = FindStrategyWinner(
                eventType,
                strategies,
                applicableDeclarations,
                ambiguities);

            if (winner >= 0)
            {
                selected.Add(eventType, strategies[winner].StrategyType);
                reachedEvents[winner].Add(eventType);
            }
            else if (winner == EventStrategyResolution.Ambiguous)
            {
                selected.Add(eventType, null);
            }
            else if (globalWinner >= 0)
            {
                selected.Add(eventType, strategies[globalWinner].StrategyType);
                reachedEvents[globalWinner].Add(eventType);
            }
            else if (globalWinner == EventStrategyResolution.Ambiguous)
            {
                selected.Add(eventType, null);
            }
            else
            {
                selected.Add(eventType, typeof(SequentialPublishStrategy));
            }
        }

        var models = new EventStrategyModel[strategies.Count];
        for (int i = 0; i < models.Length; i++)
        {
            EventStrategyInput strategy = strategies[i];
            models[i] = new EventStrategyModel(
                strategy.DeclaredEventType,
                strategy.StrategyType,
                strategy.Lifetime,
                [.. reachedEvents[i]]);
        }

        return new EventStrategyResolution(
            selected,
            models,
            applicableDeclarations,
            [.. ambiguities]);
    }

    private static int FindGlobalWinner(IReadOnlyList<EventStrategyInput> strategies)
    {
        int winner = EventStrategyResolution.None;
        for (int i = 0; i < strategies.Count; i++)
        {
            EventStrategyInput strategy = strategies[i];
            if (strategy.DeclaredEventType is not null)
                continue;

            if (winner == EventStrategyResolution.None)
            {
                winner = i;
                continue;
            }

            if (strategies[winner].StrategyType != strategy.StrategyType)
                return EventStrategyResolution.Ambiguous;
        }

        return winner;
    }

    private static int FindStrategyWinner(
        Type eventType,
        IReadOnlyList<EventStrategyInput> strategies,
        bool[] applicableDeclarations,
        List<EventStrategyAmbiguity> ambiguities)
    {
        var applicable = new List<int>();
        int bestTier = int.MaxValue;
        for (int i = 0; i < strategies.Count; i++)
        {
            Type? declaredEventType = strategies[i].DeclaredEventType;
            if (declaredEventType is null || !declaredEventType.IsAssignableFrom(eventType))
                continue;

            applicableDeclarations[i] = true;
            int tier = EventTypeSpecificity.GetTier(eventType, declaredEventType);
            if (tier < bestTier)
            {
                bestTier = tier;
                applicable.Clear();
            }

            if (tier == bestTier)
                applicable.Add(i);
        }

        if (applicable.Count == 0)
            return EventStrategyResolution.None;

        List<int> winners = bestTier == EventTypeSpecificity.EventInterfaceTier
            ? FindMostDerivedInterfaces(applicable, strategies)
            : FindClosestDeclarations(eventType, applicable, strategies);

        if (winners.Count == 1 || SameTargetAndStrategy(winners, strategies))
            return winners[0];

        if (!SameTarget(winners, strategies))
            ambiguities.Add(new EventStrategyAmbiguity(eventType, [.. winners]));

        return EventStrategyResolution.Ambiguous;
    }

    private static List<int> FindClosestDeclarations(
        Type eventType,
        List<int> candidates,
        IReadOnlyList<EventStrategyInput> strategies)
    {
        var winners = new List<int>();
        int bestSpecificity = int.MaxValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            int candidate = candidates[i];
            Type declaredEventType = strategies[candidate].DeclaredEventType!;
            int specificity = EventTypeSpecificity.GetDeliverySpecificity(
                eventType, declaredEventType);
            if (specificity < bestSpecificity)
            {
                bestSpecificity = specificity;
                winners.Clear();
            }

            if (specificity == bestSpecificity)
                winners.Add(candidate);
        }

        return winners;
    }

    private static List<int> FindMostDerivedInterfaces(
        List<int> candidates,
        IReadOnlyList<EventStrategyInput> strategies)
    {
        var winners = new List<int>();
        for (int i = 0; i < candidates.Count; i++)
        {
            int candidate = candidates[i];
            Type candidateType = strategies[candidate].DeclaredEventType!;
            bool outranked = false;
            for (int j = 0; j < candidates.Count; j++)
            {
                if (i == j)
                    continue;

                Type otherType = strategies[candidates[j]].DeclaredEventType!;
                if (candidateType != otherType && candidateType.IsAssignableFrom(otherType))
                {
                    outranked = true;
                    break;
                }
            }

            if (!outranked)
                winners.Add(candidate);
        }

        return winners;
    }

    private static bool SameTargetAndStrategy(
        List<int> candidates,
        IReadOnlyList<EventStrategyInput> strategies)
    {
        EventStrategyInput first = strategies[candidates[0]];
        for (int i = 1; i < candidates.Count; i++)
        {
            EventStrategyInput candidate = strategies[candidates[i]];
            if (candidate.DeclaredEventType != first.DeclaredEventType
                || candidate.StrategyType != first.StrategyType)
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameTarget(
        List<int> candidates,
        IReadOnlyList<EventStrategyInput> strategies)
    {
        Type? target = strategies[candidates[0]].DeclaredEventType;
        for (int i = 1; i < candidates.Count; i++)
        {
            if (strategies[candidates[i]].DeclaredEventType != target)
                return false;
        }

        return true;
    }

    private static List<Type> BuildEventTypes(
        IReadOnlyList<Type> knownEvents,
        IReadOnlyList<EventSubscriptionInput> subscriptions)
    {
        var eventTypes = new List<Type>();
        var seen = new HashSet<Type>();

        for (int i = 0; i < knownEvents.Count; i++)
            AddConcreteClosedEvent(knownEvents[i], seen, eventTypes);

        for (int i = 0; i < subscriptions.Count; i++)
            AddConcreteClosedEvent(subscriptions[i].DeclaredEventType, seen, eventTypes);

        return eventTypes;
    }

    // The one answer for which types the closure can hold, shared with the model builder so the
    // builder cannot accept a type the freeze would drop.
    internal static bool IsConcreteClosedEvent(Type eventType)
        => !eventType.IsInterface
            && !eventType.IsAbstract
            && !eventType.ContainsGenericParameters
            && typeof(IEvent).IsAssignableFrom(eventType);

    // The declared side of a subscription, which IEventHandler<TEvent> closes over: an interface,
    // an abstract base, or an open generic all pass here, where a known event would not.
    internal static bool IsEventContract(Type declaredEventType)
    {
        if (declaredEventType == typeof(IEvent))
            return true;

        foreach (var iface in declaredEventType.GetInterfaces())
        {
            if (iface == typeof(IEvent))
                return true;
        }

        return false;
    }

    private static void AddConcreteClosedEvent(
        Type eventType, HashSet<Type> seen, List<Type> eventTypes)
    {
        if (!IsConcreteClosedEvent(eventType) || !seen.Add(eventType))
            return;

        eventTypes.Add(eventType);
    }

    private static int CompareApplicable(ApplicableSubscription left, ApplicableSubscription right)
    {
        int comparison = left.Tier.CompareTo(right.Tier);
        if (comparison != 0)
            return comparison;

        comparison = left.Specificity.CompareTo(right.Specificity);
        if (comparison != 0)
            return comparison;

        comparison = string.Compare(
            left.Subscription.HandlerType.AssemblyQualifiedName,
            right.Subscription.HandlerType.AssemblyQualifiedName,
            StringComparison.Ordinal);
        if (comparison != 0)
            return comparison;

        return string.Compare(
            left.Subscription.DeclaredEventType.AssemblyQualifiedName,
            right.Subscription.DeclaredEventType.AssemblyQualifiedName,
            StringComparison.Ordinal);
    }

    private readonly struct ApplicableSubscription
    {
        public ApplicableSubscription(
            Type eventType, EventSubscriptionInput subscription, int subscriptionIndex)
        {
            Subscription = subscription;
            SubscriptionIndex = subscriptionIndex;

            Type declaredEventType = subscription.DeclaredEventType;
            Tier = EventTypeSpecificity.GetTier(eventType, declaredEventType);
            Specificity = Tier is EventTypeSpecificity.BaseClassTier
                or EventTypeSpecificity.EventInterfaceTier
                ? EventTypeSpecificity.GetDeliverySpecificity(eventType, declaredEventType)
                : 0;
        }

        public EventSubscriptionInput Subscription { get; }

        public int SubscriptionIndex { get; }

        public int Tier { get; }

        public int Specificity { get; }
    }
}

internal sealed class EventClosureResult
{
    public EventClosureResult(
        EventModel[] events,
        EventSubscriptionModel[] eventSubscriptions,
        EventStrategyModel[] eventStrategies,
        EventStrategyResolution strategyResolution)
    {
        Events = events ?? throw new ArgumentNullException(nameof(events));
        EventSubscriptions = eventSubscriptions
            ?? throw new ArgumentNullException(nameof(eventSubscriptions));
        EventStrategies = eventStrategies ?? throw new ArgumentNullException(nameof(eventStrategies));
        StrategyResolution = strategyResolution
            ?? throw new ArgumentNullException(nameof(strategyResolution));
    }

    public EventModel[] Events { get; }

    public EventSubscriptionModel[] EventSubscriptions { get; }

    public EventStrategyModel[] EventStrategies { get; }

    public EventStrategyResolution StrategyResolution { get; }
}

internal sealed class EventStrategyResolution
{
    public const int None = -1;
    public const int Ambiguous = -2;

    private readonly Dictionary<Type, Type?> _strategies;

    public EventStrategyResolution(
        Dictionary<Type, Type?> strategies,
        EventStrategyModel[] models,
        bool[] applicableDeclarations,
        EventStrategyAmbiguity[] ambiguities)
    {
        _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
        Models = models ?? throw new ArgumentNullException(nameof(models));
        ApplicableDeclarations = applicableDeclarations
            ?? throw new ArgumentNullException(nameof(applicableDeclarations));
        Ambiguities = ambiguities ?? throw new ArgumentNullException(nameof(ambiguities));
    }

    public EventStrategyModel[] Models { get; }

    public bool[] ApplicableDeclarations { get; }

    public EventStrategyAmbiguity[] Ambiguities { get; }

    public Type? GetStrategy(Type eventType) => _strategies[eventType];
}

internal sealed class EventStrategyAmbiguity
{
    public EventStrategyAmbiguity(Type eventType, int[] declarationIndexes)
    {
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        DeclarationIndexes = declarationIndexes
            ?? throw new ArgumentNullException(nameof(declarationIndexes));
    }

    public Type EventType { get; }

    public int[] DeclarationIndexes { get; }
}

internal readonly struct EventSubscriptionInput
{
    public EventSubscriptionInput(
        Type handlerType,
        Type declaredEventType,
        RequestFlowLifetime lifetime)
    {
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
        DeclaredEventType = declaredEventType
            ?? throw new ArgumentNullException(nameof(declaredEventType));
        Lifetime = lifetime;
    }

    public Type HandlerType { get; }

    public Type DeclaredEventType { get; }

    public RequestFlowLifetime Lifetime { get; }
}

internal readonly struct EventStrategyInput
{
    public EventStrategyInput(
        Type? declaredEventType,
        Type strategyType,
        RequestFlowLifetime lifetime)
    {
        DeclaredEventType = declaredEventType;
        StrategyType = strategyType ?? throw new ArgumentNullException(nameof(strategyType));
        Lifetime = lifetime;
    }

    public Type? DeclaredEventType { get; }

    public Type StrategyType { get; }

    public RequestFlowLifetime Lifetime { get; }
}
