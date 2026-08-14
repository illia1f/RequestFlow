using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RequestFlow;

/// <summary>
/// One request type, the handlers covering it, and the stage chain it would run.
/// </summary>
/// <remarks>
/// Building the chain needs the response or item type, and only a handler supplies it, so a
/// request no handler covers always has an empty <see cref="Stages"/>. Read that empty list as
/// "no handler to build a chain against", not as "no stage applies". A request several handlers
/// cover, which the built-in rules report on, holds what every one of them closes.
/// </remarks>
public sealed class RequestModel
{
    /// <exception cref="ArgumentNullException"/>
    internal RequestModel(Type requestType, HandlerModel[] handlers, ClosedStageModel[] stages)
    {
        RequestType = requestType ?? throw new ArgumentNullException(nameof(requestType));
        Handlers = new ReadOnlyCollection<HandlerModel>(
            handlers ?? throw new ArgumentNullException(nameof(handlers)));
        Stages = new ReadOnlyCollection<ClosedStageModel>(
            stages ?? throw new ArgumentNullException(nameof(stages)));
    }

    /// <summary>
    /// The request type as registered, always closed: <c>PlaceOrder</c>, never an open definition.
    /// </summary>
    public Type RequestType { get; }

    /// <summary>
    /// The handlers covering this request, normally one. None and several are both possible.
    /// </summary>
    public IReadOnlyList<HandlerModel> Handlers { get; }

    /// <summary>
    /// The stages closed for this request, in the order they wrap the handler. One chain while one
    /// handler covers the request, which is what every registration that starts produces.
    /// </summary>
    public IReadOnlyList<ClosedStageModel> Stages { get; }
}
