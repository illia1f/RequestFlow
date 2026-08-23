using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

internal static class EventPublishStrategies
{
    public static Task SequentialAsync(
        EventDelivery delivery,
        CancellationToken cancellationToken,
        bool stopOnFirstFailure)
        => WalkSequentiallyAsync(delivery, cancellationToken, stopOnFirstFailure);

    public static Task ParallelAsync(
        EventDelivery delivery,
        CancellationToken cancellationToken)
        => FanOutAsync(delivery, cancellationToken);

    private static async Task WalkSequentiallyAsync(
        EventDelivery delivery,
        CancellationToken cancellationToken,
        bool stopOnFirstFailure)
    {
        if (cancellationToken.IsCancellationRequested)
            throw delivery.Canceled(null, delivery.Count, cancellationToken);

        List<EventHandlerFailure>? failures = null;
        for (int i = 0; i < delivery.Count; i++)
        {
            if (i > 0 && cancellationToken.IsCancellationRequested)
            {
                throw delivery.Canceled(
                    failures,
                    delivery.Count - i,
                    cancellationToken);
            }

            EventHandlerFailure? failure = await delivery.StartAsync(i, cancellationToken)
                .ConfigureAwait(false);
            if (failure is null)
                continue;

            failures ??= [];
            failures.Add(failure);

            if (stopOnFirstFailure)
                delivery.ThrowIfAny(failures, delivery.Count - i - 1);
        }

        delivery.ThrowIfAny(failures);
    }

    private static async Task FanOutAsync(
        EventDelivery delivery,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            throw delivery.Canceled(null, delivery.Count, cancellationToken);

        var tasks = new Task<EventHandlerFailure?>[delivery.Count];
        for (int i = 0; i < tasks.Length; i++)
            tasks[i] = delivery.StartAsync(i, cancellationToken);

        List<EventHandlerFailure>? failures = null;
        for (int i = 0; i < tasks.Length; i++)
        {
            EventHandlerFailure? failure = await tasks[i].ConfigureAwait(false);
            if (failure is null)
                continue;

            failures ??= [];
            failures.Add(failure);
        }

        delivery.ThrowIfAny(failures);
    }
}
