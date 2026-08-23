using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RequestFlow;

/// <summary>
/// One event publish strategy declaration and the known events it selects.
/// </summary>
public sealed class EventStrategyModel
{
    /// <exception cref="ArgumentNullException"/>
    internal EventStrategyModel(
        Type? declaredEventType,
        Type strategyType,
        RequestFlowLifetime lifetime,
        Type[] reachedEvents)
    {
        DeclaredEventType = declaredEventType;
        StrategyType = strategyType ?? throw new ArgumentNullException(nameof(strategyType));
        Lifetime = lifetime;
        ReachedEvents = new ReadOnlyCollection<Type>(
            reachedEvents ?? throw new ArgumentNullException(nameof(reachedEvents)));
    }

    /// <summary>
    /// The event contract the declaration targets, or null for the global fallback.
    /// </summary>
    public Type? DeclaredEventType { get; }

    /// <summary>
    /// The strategy type named by the declaration.
    /// </summary>
    public Type StrategyType { get; }

    /// <summary>
    /// The lifetime configured for the strategy.
    /// </summary>
    public RequestFlowLifetime Lifetime { get; }

    /// <summary>
    /// The known events for which this declaration is the winning strategy.
    /// </summary>
    public IReadOnlyList<Type> ReachedEvents { get; }
}
