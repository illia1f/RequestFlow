using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Publishes event entries one at a time and stops after the first failure.
/// </summary>
public sealed class FailFastPublishStrategy : IEventPublishStrategy
{
    /// <inheritdoc/>
    public Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken)
        => EventPublishStrategies.SequentialAsync(
            delivery, cancellationToken, stopOnFirstFailure: true);
}
