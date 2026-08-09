using RequestFlow.Cqrs;

namespace Orders.Api.Orders;

public sealed class CreateOrderCommandHandler(OrderStore store)
    : ICommandHandler<CreateOrderCommand, Guid>, IOrdersCommandHandler
{
    public Task<Guid> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        Order order = new(Guid.NewGuid(), command.Customer, command.Total, Cancelled: false);
        store.Save(order);
        return Task.FromResult(order.Id);
    }
}
