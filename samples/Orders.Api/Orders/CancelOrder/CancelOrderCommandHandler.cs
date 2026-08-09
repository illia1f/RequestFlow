using RequestFlow.Cqrs;

namespace Orders.Api.Orders;

public sealed class CancelOrderCommandHandler(OrderStore store)
    : ICommandHandler<CancelOrderCommand>, IOrdersCommandHandler
{
    public Task HandleAsync(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        if (!store.TryCancel(command.Id))
            throw new OrderNotFoundException(command.Id);

        return Task.CompletedTask;
    }
}
