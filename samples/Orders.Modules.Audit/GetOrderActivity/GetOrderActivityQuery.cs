using RequestFlow.Cqrs;

namespace Orders.Modules.Audit;

public sealed record OrderActivity(int Placed, int Cancelled, IReadOnlyList<string> Audit);

public sealed record GetOrderActivityQuery : IQuery<OrderActivity>;
