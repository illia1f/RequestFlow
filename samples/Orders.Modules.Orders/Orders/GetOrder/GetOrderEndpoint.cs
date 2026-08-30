using Microsoft.AspNetCore.Http.HttpResults;
using RequestFlow.Cqrs;

namespace Orders.Modules.Orders;

internal static class GetOrderEndpoint
{
    public static void MapGetOrder(this IEndpointRouteBuilder routes)
        => routes.MapGet("/orders/{id:guid}", async Task<Results<Ok<OrderDto>, NotFound>> (
            Guid id, IQueryDispatcher queries, CancellationToken cancellationToken) =>
        {
            OrderDto? order = await queries.SendAsync(new GetOrderQuery(id), cancellationToken);
            if (order is null)
                return TypedResults.NotFound();

            return TypedResults.Ok(order);
        });
}
