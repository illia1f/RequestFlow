namespace Orders.Api.Orders;

public sealed record OrderDto(Guid Id, string Customer, decimal Total, string Status);
