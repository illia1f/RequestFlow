using System;

namespace RequestFlow;

/// <summary>
/// Identifies an event handler failure and the event contract that was invoked.
/// </summary>
public sealed class EventHandlerFailure
{
    /// <exception cref="ArgumentNullException"/>
    public EventHandlerFailure(Type handlerType, Type declaredEventType, Exception exception)
    {
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
        DeclaredEventType = declaredEventType ?? throw new ArgumentNullException(nameof(declaredEventType));
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
    }

    /// <summary>
    /// The type of the handler that failed.
    /// </summary>
    public Type HandlerType { get; }

    /// <summary>
    /// The event contract the handler was invoked for.
    /// </summary>
    public Type DeclaredEventType { get; }

    /// <summary>
    /// The exception thrown by the handler.
    /// </summary>
    public Exception Exception { get; }
}
