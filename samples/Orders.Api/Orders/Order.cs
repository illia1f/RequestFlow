namespace Orders.Api.Orders;

public sealed record Order(Guid Id, string Customer, decimal Total, bool Cancelled);
