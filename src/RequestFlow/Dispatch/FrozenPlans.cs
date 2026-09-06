using System;
using System.Collections.Generic;

namespace RequestFlow;

internal sealed class FrozenPlans(
    DispatchMap dispatch, EventMap events, Dictionary<Type, RequestPipeline> pipelines)
{
    public DispatchMap Dispatch { get; } = dispatch;

    public EventMap Events { get; } = events;

    public RequestPipeline GetPipeline(Type requestType)
        => pipelines.TryGetValue(requestType, out RequestPipeline? pipeline)
            ? pipeline
            : throw new HandlerNotFoundException(requestType);
}
