using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Publishes events through their selected strategies.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes <paramref name="event"/> through its selected strategy.
    /// </summary>
    Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default);
}
