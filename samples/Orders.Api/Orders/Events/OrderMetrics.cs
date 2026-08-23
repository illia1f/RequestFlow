using RequestFlow;

namespace Orders.Api.Orders;

/// <summary>
/// Counts placed and cancelled orders. OrderPlaced publishes in parallel, so the writes are interlocked.
/// </summary>
public sealed class OrderMetrics
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
/// One class with two subscriptions: each contract is invoked at its own position in the event plan.
/// </summary>
public sealed class UpdateOrderMetrics(OrderMetrics metrics)
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
