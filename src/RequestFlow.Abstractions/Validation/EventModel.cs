using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RequestFlow;

/// <summary>
/// One known event type and the handlers that receive it in delivery order.
/// </summary>
public sealed class EventModel
{
    /// <exception cref="ArgumentNullException"/>
    internal EventModel(Type eventType, EventHandlerModel[] handlers)
        : this(eventType, handlers, typeof(SequentialPublishStrategy))
    { }

    /// <exception cref="ArgumentNullException"/>
    internal EventModel(Type eventType, EventHandlerModel[] handlers, Type? publishStrategy)
    {
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        Handlers = new ReadOnlyCollection<EventHandlerModel>(
            handlers ?? throw new ArgumentNullException(nameof(handlers)));
        PublishStrategy = publishStrategy;
    }

    /// <summary>
    /// The concrete, closed event type.
    /// </summary>
    public Type EventType { get; }

    /// <summary>
    /// The handlers that receive this event, in frozen delivery order.
    /// </summary>
    public IReadOnlyList<EventHandlerModel> Handlers { get; }

    /// <summary>
    /// The strategy selected for this event, or null when declarations are ambiguous.
    /// </summary>
    public Type? PublishStrategy { get; }
}
