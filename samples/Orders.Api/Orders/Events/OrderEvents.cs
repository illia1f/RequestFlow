using RequestFlow;

namespace Orders.Api.Orders;

/// <summary>
/// Base of the order event family. AuditOrderEvents subscribes here and receives every derived event.
/// </summary>
public abstract record OrderEvent : IEvent;

public sealed record OrderPlaced(Guid OrderId, string Customer, decimal Total) : OrderEvent;

public sealed record OrderCancelled(Guid OrderId) : OrderEvent;
