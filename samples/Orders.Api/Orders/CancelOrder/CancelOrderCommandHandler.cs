using RequestFlow;
using RequestFlow.Cqrs;

namespace Orders.Api.Orders;

public sealed class CancelOrderCommandHandler(OrderStore store, IEventPublisher events)
    : ICommandHandler<CancelOrderCommand>, IOrdersCommandHandler
{
    public async Task HandleAsync(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        if (!store.TryCancel(command.Id))
            throw new OrderNotFoundException(command.Id);

        await events.PublishAsync(new OrderCancelled(command.Id), cancellationToken);
    }
}
