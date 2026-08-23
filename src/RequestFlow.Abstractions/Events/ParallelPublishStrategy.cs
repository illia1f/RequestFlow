using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Starts every event entry before awaiting them in frozen entry order.
/// </summary>
public sealed class ParallelPublishStrategy : IEventPublishStrategy
{
    /// <inheritdoc/>
    public Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken)
        => EventPublishStrategies.ParallelAsync(delivery, cancellationToken);
}
