using Microsoft.AspNetCore.Http.HttpResults;
using RequestFlow.Cqrs;

namespace Orders.Modules.Orders;

internal static class CancelOrderEndpoint
{
    // The exception handler maps an unknown order to this 404 response.
    public static void MapCancelOrder(this IEndpointRouteBuilder routes)
        => routes.MapPost("/orders/{id:guid}/cancel", async Task<NoContent> (
                Guid id, ICommandDispatcher commands, CancellationToken cancellationToken) =>
            {
                await commands.SendAsync(new CancelOrderCommand(id), cancellationToken);
                return TypedResults.NoContent();
            })
            .Produces(StatusCodes.Status404NotFound);
}
