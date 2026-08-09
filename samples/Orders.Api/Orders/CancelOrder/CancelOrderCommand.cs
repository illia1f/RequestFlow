using RequestFlow.Cqrs;

namespace Orders.Api.Orders;

public sealed record CancelOrderCommand(Guid Id) : ICommand;
