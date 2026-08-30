using Orders.Modules.Orders;
using RequestFlow;

namespace Orders.Modules.Audit;

/// <summary>
/// The singleton accepts concurrent event publications, so writes are interlocked.
/// </summary>
internal sealed class OrderMetrics
{
    private int _placed;
    private int _cancelled;

    public int Placed => Volatile.Read(ref _placed);

    public int Cancelled => Volatile.Read(ref _cancelled);

    public void CountPlaced()
        => Interlocked.Increment(ref _placed);

    public void CountCancelled()
        => Interlocked.Increment(ref _cancelled);
}

/// <summary>
/// Handles both order event contracts, which RequestFlow invokes separately.
/// </summary>
internal sealed class UpdateOrderMetrics(OrderMetrics metrics)
    : IEventHandler<OrderPlaced>, IEventHandler<OrderCancelled>
{
    public Task HandleAsync(OrderPlaced placed, CancellationToken cancellationToken)
    {
        metrics.CountPlaced();
        return Task.CompletedTask;
    }

    public Task HandleAsync(OrderCancelled cancelled, CancellationToken cancellationToken)
    {
        metrics.CountCancelled();
        return Task.CompletedTask;
    }
}
