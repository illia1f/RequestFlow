using RequestFlow.Cqrs;

namespace Orders.Api.Orders;

public sealed record GetOrderQuery(Guid Id) : IQuery<OrderDto?>;
