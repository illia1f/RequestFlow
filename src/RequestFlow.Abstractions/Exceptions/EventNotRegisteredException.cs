using System;

namespace RequestFlow;

/// <summary>
/// Thrown when a published event type has no entry in the event map.
/// </summary>
/// <exception cref="ArgumentNullException"/>
public sealed class EventNotRegisteredException(Type eventType) : InvalidOperationException(BuildMessage(eventType))
{
    /// <summary>
    /// The event type that was not registered.
    /// </summary>
    public Type EventType { get; } = eventType;

    private static string BuildMessage(Type eventType)
    {
        if (eventType is null)
            throw new ArgumentNullException(nameof(eventType));

        return $"Event type '{eventType.FullName}' has no entry in the event map. Make sure its " +
            "assembly is included in RegisterHandlersFromAssembly* during AddRequestFlow; a derived " +
            "type or proxy that was never scanned stays unknown even when its base event is " +
            "registered.";
    }
}
