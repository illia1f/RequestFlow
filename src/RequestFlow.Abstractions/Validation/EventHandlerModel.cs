using System;

namespace RequestFlow;

/// <summary>
/// One event handler delivery in a known event's frozen plan.
/// </summary>
public sealed class EventHandlerModel
{
    /// <exception cref="ArgumentNullException"/>
    internal EventHandlerModel(
        Type handlerType, Type declaredEventType, RequestFlowLifetime lifetime)
    {
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
        DeclaredEventType = declaredEventType
            ?? throw new ArgumentNullException(nameof(declaredEventType));
        Lifetime = lifetime;
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
}
