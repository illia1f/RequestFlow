using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Handles published events of a single type.
/// </summary>
/// <typeparam name="TEvent">The event handled.</typeparam>
public interface IEventHandler<in TEvent>
    where TEvent : IEvent
{
    /// <summary>
    /// Handles <paramref name="event"/>.
    /// </summary>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}
