using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

internal sealed class ParallelEventPlan(EventModel model, HandlerEntry[] entries)
    : EventPlan(model, entries)
{
    public override async Task ExecuteAsync(
        IEvent @event,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        ThrowIfCanceledBeforePublication(cancellationToken);

        var tasks = new Task[Entries.Length];
        for (int i = 0; i < tasks.Length; i++)
            tasks[i] = Start(i, @event, services, cancellationToken);

        List<EventHandlerFailure>? failures = null;
        for (int i = 0; i < tasks.Length; i++)
        {
            Task task = tasks[i];

            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures ??= [];
                failures.Add(FailureFrom(i, task, exception));
            }
        }

        ThrowIfAnyFailed(failures);
    }
}
