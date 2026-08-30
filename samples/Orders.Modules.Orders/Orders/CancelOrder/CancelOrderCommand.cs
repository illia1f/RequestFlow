using RequestFlow.Cqrs;

namespace Orders.Modules.Orders;

public sealed record CancelOrderCommand(Guid Id) : ICommand;
