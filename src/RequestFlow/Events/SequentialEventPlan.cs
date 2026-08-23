using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

internal sealed class SequentialEventPlan(
    EventModel model,
    HandlerEntry[] entries,
    bool stopOnFirstFailure = false)
    : EventPlan(model, entries)
{
    public override async Task ExecuteAsync(
        IEvent @event,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        ThrowIfCanceledBeforePublication(cancellationToken);

        List<EventHandlerFailure>? failures = null;
        for (int i = 0; i < Entries.Length; i++)
        {
            if (i > 0 && cancellationToken.IsCancellationRequested)
            {
                throw new EventPublishCanceledException(
                    EventType, cancellationToken, failures ?? NoFailures, Entries.Length - i);
            }

            Task task = Start(i, @event, services, cancellationToken);
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures ??= [];
                failures.Add(FailureFrom(i, task, exception));

                if (stopOnFirstFailure)
                    ThrowIfAnyFailed(failures, Entries.Length - i - 1);
            }
        }

        ThrowIfAnyFailed(failures);
    }
}
