using Microsoft.AspNetCore.Http.HttpResults;
using RequestFlow.Cqrs;

namespace Orders.Api.Orders;

internal static class CancelOrderEndpoint
{
    // Produces covers the 404 the exception handler writes for an id the store does not hold.
    public static void MapCancelOrder(this IEndpointRouteBuilder routes)
        => routes.MapPost("/orders/{id:guid}/cancel", async Task<NoContent> (
                Guid id, ICommandDispatcher commands, CancellationToken cancellationToken) =>
            {
                await commands.SendAsync(new CancelOrderCommand(id), cancellationToken);
                return TypedResults.NoContent();
            })
            .Produces(StatusCodes.Status404NotFound);
}
