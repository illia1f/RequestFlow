using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

internal sealed class EventPublisher(EventMap map, IServiceProvider services) : IEventPublisher
{
    private readonly EventMap _map = map ?? throw new ArgumentNullException(nameof(map));
    private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));

    public Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        if (@event is null)
            throw new ArgumentNullException(nameof(@event));

        Type eventType = @event.GetType();
        if (!_map.TryGetPlanFor(eventType, out EventPlan? plan))
            throw new EventNotRegisteredException(eventType);

        return plan!.ExecuteAsync(@event, _services, cancellationToken);
    }
}
