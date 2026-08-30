using System.Collections.Concurrent;
using Orders.Modules.Orders;
using RequestFlow;

namespace Orders.Modules.Audit;

/// <summary>
/// Keeps one line per published order event, oldest first.
/// </summary>
internal sealed class OrderAuditTrail
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyList<string> Entries => [.. _entries];

    public void Record(string entry)
        => _entries.Enqueue(entry);
}

/// <summary>
/// Subscribes to the OrderEvent base, so every derived event lands here without an exact-type subscription.
/// </summary>
internal sealed class AuditOrderEvents(OrderAuditTrail trail) : IEventHandler<OrderEvent>
{
    public Task HandleAsync(OrderEvent @event, CancellationToken cancellationToken)
    {
        trail.Record(@event switch
        {
            OrderPlaced placed => $"placed {placed.OrderId} for {placed.Customer}, total {placed.Total}",
            OrderCancelled cancelled => $"cancelled {cancelled.OrderId}",
            _ => $"unrecognized {@event}",
        });

        return Task.CompletedTask;
    }
}
