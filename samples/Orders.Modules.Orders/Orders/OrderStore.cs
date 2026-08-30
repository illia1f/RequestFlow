using System.Collections.Concurrent;

namespace Orders.Modules.Orders;

internal sealed class OrderStore
{
    private readonly ConcurrentDictionary<Guid, Order> _orders = new();

    public void Save(Order order)
        => _orders[order.Id] = order;

    public Order? Find(Guid id)
        => _orders.TryGetValue(id, out Order? order) ? order : null;

    // Find and Save as two calls would drop a concurrent write, so the update only lands while the order still matches what this call read.
    public bool TryCancel(Guid id)
    {
        while (_orders.TryGetValue(id, out Order? current))
        {
            if (_orders.TryUpdate(id, current with { Cancelled = true }, current))
                return true;
        }

        return false;
    }
}
