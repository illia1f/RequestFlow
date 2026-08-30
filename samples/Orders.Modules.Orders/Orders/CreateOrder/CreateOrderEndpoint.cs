using Microsoft.AspNetCore.Http.HttpResults;
using Orders.Modules.Orders.Validation;
using RequestFlow.Cqrs;

namespace Orders.Modules.Orders;

public sealed record OrderCreated(Guid Id);

internal static class CreateOrderEndpoint
{
    public static void MapCreateOrder(this IEndpointRouteBuilder routes)
        => routes.MapPost("/orders", async Task<Created<OrderCreated>> (
                CreateOrderCommand command, ICommandDispatcher commands, CancellationToken cancellationToken) =>
            {
                Guid id = await commands.SendAsync(command, cancellationToken);
                return TypedResults.Created($"/orders/{id}", new OrderCreated(id));
            })
            .Produces<ValidationErrors>(StatusCodes.Status400BadRequest);
}
