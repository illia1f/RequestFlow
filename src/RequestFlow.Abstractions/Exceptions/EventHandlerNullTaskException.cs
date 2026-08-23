using System;

namespace RequestFlow;

/// <summary>
/// Thrown when an event handler returns a null task from <c>HandleAsync</c>.
/// </summary>
public sealed class EventHandlerNullTaskException : NullTaskException
{
    /// <exception cref="ArgumentNullException"/>
    public EventHandlerNullTaskException(Type eventType, Type handlerType)
        : base(BuildMessage(eventType, handlerType))
    {
        EventType = eventType;
        HandlerType = handlerType;
    }

    /// <summary>
    /// The event type being handled.
    /// </summary>
    public Type EventType { get; }

    /// <summary>
    /// The type of the handler that returned the null task.
    /// </summary>
    public Type HandlerType { get; }

    private static string BuildMessage(Type eventType, Type handlerType)
    {
        if (eventType is null)
            throw new ArgumentNullException(nameof(eventType));

        if (handlerType is null)
            throw new ArgumentNullException(nameof(handlerType));

        return $"The handler '{handlerType.FullName}' for event '{eventType.FullName}' returned a null task from HandleAsync.";
    }
}
