namespace RequestFlow;

internal sealed class FrozenPlans(DispatchMap dispatch, EventMap events)
{
    public DispatchMap Dispatch { get; } = dispatch;

    public EventMap Events { get; } = events;
}
