using RequestFlow.Cqrs;

namespace Orders.Api.Orders;

public sealed class GetOrderQueryHandler(OrderStore store) : IQueryHandler<GetOrderQuery, OrderDto?>
{
    public Task<OrderDto?> HandleAsync(GetOrderQuery query, CancellationToken cancellationToken)
    {
        Order? order = store.Find(query.Id);
        OrderDto? dto = order is null
            ? null
            : new OrderDto(order.Id, order.Customer, order.Total, order.Cancelled ? "cancelled" : "open");

        return Task.FromResult(dto);
    }
}
