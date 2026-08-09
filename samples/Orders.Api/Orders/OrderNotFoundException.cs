namespace Orders.Api.Orders;

/// <summary>
/// Thrown when a command names an order the store does not hold.
/// </summary>
public sealed class OrderNotFoundException(Guid id) : Exception($"Order '{id}' does not exist.")
{
    public Guid Id { get; } = id;
}
