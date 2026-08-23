using System;

namespace RequestFlow;

/// <summary>
/// Identifies one handler entry in an event publish.
/// </summary>
public readonly struct EventSubscription
{
    /// <exception cref="ArgumentNullException"/>
    public EventSubscription(Type handlerType, Type declaredEventType)
    {
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
        DeclaredEventType = declaredEventType
            ?? throw new ArgumentNullException(nameof(declaredEventType));
    }

    /// <summary>
    /// The type of the handler invoked by the entry.
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// The event contract the handler is invoked for.
    /// </summary>
    public Type DeclaredEventType { get; }
}
