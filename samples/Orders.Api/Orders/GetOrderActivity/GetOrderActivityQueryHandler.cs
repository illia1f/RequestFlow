using RequestFlow.Cqrs;

namespace Orders.Api.Orders;

public sealed class GetOrderActivityQueryHandler(OrderMetrics metrics, OrderAuditTrail trail)
    : IQueryHandler<GetOrderActivityQuery, OrderActivity>
{
    public Task<OrderActivity> HandleAsync(GetOrderActivityQuery query, CancellationToken cancellationToken)
        => Task.FromResult(new OrderActivity(metrics.Placed, metrics.Cancelled, trail.Entries));
}
