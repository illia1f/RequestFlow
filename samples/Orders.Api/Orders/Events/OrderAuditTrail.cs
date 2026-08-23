using System.Collections.Concurrent;
using RequestFlow;

namespace Orders.Api.Orders;

/// <summary>
/// Keeps one line per published order event, oldest first.
/// </summary>
public sealed class OrderAuditTrail
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyList<string> Entries => [.. _entries];

    public void Record(string entry)
        => _entries.Enqueue(entry);
}

/// <summary>
/// Subscribes to the OrderEvent base, so every derived event lands here without an exact-type subscription.
/// </summary>
public sealed class AuditOrderEvents(OrderAuditTrail trail) : IEventHandler<OrderEvent>
{
    public Task HandleAsync(OrderEvent @event, CancellationToken cancellationToken)
    {
        // The fallback arm keeps a future OrderEvent from failing the whole publish here.
        trail.Record(@event switch
        {
            OrderPlaced placed => $"placed {placed.OrderId} for {placed.Customer}, total {placed.Total}",
            OrderCancelled cancelled => $"cancelled {cancelled.OrderId}",
            _ => $"unrecognized {@event}",
        });

        return Task.CompletedTask;
    }
}
