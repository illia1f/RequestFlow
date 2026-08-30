using RequestFlow.Cqrs;

namespace Orders.Modules.Orders;

public sealed record GetOrderQuery(Guid Id) : IQuery<OrderDto?>;
