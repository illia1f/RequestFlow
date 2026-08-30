using RequestFlow;

namespace Orders.Modules.Orders;

/// <summary>
/// Defines the order event base that the Audit module uses to receive every derived event.
/// </summary>
public abstract record OrderEvent : IEvent;

public sealed record OrderPlaced(Guid OrderId, string Customer, decimal Total) : OrderEvent;

public sealed record OrderCancelled(Guid OrderId) : OrderEvent;
