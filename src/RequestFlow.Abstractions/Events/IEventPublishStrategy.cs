using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Defines how the handler entries for one event publish are started and awaited.
/// </summary>
public interface IEventPublishStrategy
{
    /// <summary>
    /// Publishes the entries in <paramref name="delivery"/>.
    /// </summary>
    Task PublishAsync(EventDelivery delivery, CancellationToken cancellationToken);
}
