namespace Orders.Modules.Orders;

public static class OrdersEndpoints
{
    public static void MapOrders(this IEndpointRouteBuilder routes)
    {
        routes.MapCreateOrder();
        routes.MapCancelOrder();
        routes.MapGetOrder();
    }
}
