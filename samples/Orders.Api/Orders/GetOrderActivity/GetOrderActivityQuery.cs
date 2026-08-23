using RequestFlow.Cqrs;

namespace Orders.Api.Orders;

public sealed record OrderActivity(int Placed, int Cancelled, IReadOnlyList<string> Audit);

public sealed record GetOrderActivityQuery : IQuery<OrderActivity>;
