using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Publishes event entries one at a time and collects every failure.
/// </summary>
public sealed class SequentialPublishStrategy : IEventPublishStrategy
{
    /// <inheritdoc/>
    public Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken)
        => EventPublishStrategies.SequentialAsync(
            delivery, cancellationToken, stopOnFirstFailure: false);
}
