using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace RequestFlow;

internal sealed class StrategyEventPlan(
    EventModel model,
    HandlerEntry[] entries,
    Type strategyType)
    : EventPlan(model, entries)
{
    public override Task ExecuteAsync(
        IEvent @event,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        try
        {
            var strategy = (IEventPublishStrategy)services.GetRequiredService(strategyType);
            EventDelivery delivery = CreateDelivery(@event, services, cancellationToken);
            return NullTaskGuard.FromStrategy(
                strategy.PublishAsync(delivery, cancellationToken),
                strategyType);
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }
}
