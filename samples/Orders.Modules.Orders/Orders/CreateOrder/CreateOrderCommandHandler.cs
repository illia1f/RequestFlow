using RequestFlow;
using RequestFlow.Cqrs;

namespace Orders.Modules.Orders;

internal sealed class CreateOrderCommandHandler(OrderStore store, IEventPublisher events)
    : ICommandHandler<CreateOrderCommand, Guid>, IOrdersCommandHandler
{
    public async Task<Guid> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Order order = new(Guid.NewGuid(), command.Customer, command.Total, Cancelled: false);
        store.Save(order);

        await events.PublishAsync(new OrderPlaced(order.Id, order.Customer, order.Total), cancellationToken);
        return order.Id;
    }
}
