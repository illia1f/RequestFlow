using Microsoft.AspNetCore.Http.HttpResults;
using RequestFlow.Cqrs;

namespace Orders.Api.Orders;

internal static class GetOrderActivityEndpoint
{
    public static void MapGetOrderActivity(this IEndpointRouteBuilder routes)
        => routes.MapGet("/orders/activity", async Task<Ok<OrderActivity>> (
            IQueryDispatcher queries, CancellationToken cancellationToken) =>
        {
            OrderActivity activity = await queries.SendAsync(new GetOrderActivityQuery(), cancellationToken);
            return TypedResults.Ok(activity);
        });
}
