using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RequestFlow;

/// <summary>
/// One event handler subscription and the known events it reaches.
/// </summary>
public sealed class EventSubscriptionModel
{
    /// <exception cref="ArgumentNullException"/>
    internal EventSubscriptionModel(
        Type handlerType,
        Type declaredEventType,
        RequestFlowLifetime lifetime,
        Type[] reachedEvents)
    {
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
        DeclaredEventType = declaredEventType
            ?? throw new ArgumentNullException(nameof(declaredEventType));
        Lifetime = lifetime;
        ReachedEvents = new ReadOnlyCollection<Type>(
            reachedEvents ?? throw new ArgumentNullException(nameof(reachedEvents)));
    }

    /// <summary>
    /// The class implementing the handler.
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// The event type named by this handler contract.
    /// </summary>
    public Type DeclaredEventType { get; }

    /// <summary>
    /// The lifetime this handler is registered with.
    /// </summary>
    public RequestFlowLifetime Lifetime { get; }

    /// <summary>
    /// The known events this subscription receives, in event order.
    /// </summary>
    public IReadOnlyList<Type> ReachedEvents { get; }
}
