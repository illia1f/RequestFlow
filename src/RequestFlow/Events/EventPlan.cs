using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

internal abstract class EventPlan
{
    protected static readonly IReadOnlyList<EventHandlerFailure> NoFailures = [];

    private static readonly Task<EventHandlerFailure?> NoFailureTask =
        Task.FromResult<EventHandlerFailure?>(null);

    private readonly EventSubscription[] _subscriptions;
    private readonly EventDeliveryStart _deliveryStart;

    protected EventPlan(EventModel model, HandlerEntry[] entries)
    {
        if (model is null)
            throw new ArgumentNullException(nameof(model));
        if (entries is null)
            throw new ArgumentNullException(nameof(entries));

        EventType = model.EventType;
        Handlers = model.Handlers;

        if (entries.Length != Handlers.Count)
        {
            throw new ArgumentException(
                $"A plan for '{EventType.FullName}' needs one entry per handler, but got {entries.Length} for {Handlers.Count} handlers.",
                nameof(entries));
        }

        Entries = entries;
        _subscriptions = new EventSubscription[Handlers.Count];
        for (int i = 0; i < _subscriptions.Length; i++)
        {
            EventHandlerModel handler = Handlers[i];
            _subscriptions[i] = new EventSubscription(
                handler.HandlerType,
                handler.DeclaredEventType);
        }

        _deliveryStart = StartForDeliveryAsync;
    }

    public IReadOnlyList<EventHandlerModel> Handlers { get; }

    protected Type EventType { get; }

    protected HandlerEntry[] Entries { get; }

    protected void ThrowIfCanceledBeforePublication(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            throw new EventPublishCanceledException(
                EventType, cancellationToken, NoFailures, Entries.Length);
        }
    }

    protected void ThrowIfAnyFailed(
        IReadOnlyList<EventHandlerFailure>? failures,
        int skippedHandlerCount = 0)
    {
        if (failures is not null)
        {
            throw new EventPublishException(
                EventType,
                failures,
                Entries.Length,
                skippedHandlerCount);
        }
    }

    protected EventDelivery CreateDelivery(
        IEvent @event,
        IServiceProvider services,
        CancellationToken cancellationToken)
        => new(
            @event,
            services,
            _subscriptions,
            _deliveryStart,
            cancellationToken);

    protected Task Start(
        int index,
        IEvent @event,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        EventHandlerModel handler = Handlers[index];

        try
        {
            Task task = Entries[index](services, @event, cancellationToken);
            return NullTaskGuard.FromEventHandler(task, EventType, handler.HandlerType);
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    protected EventHandlerFailure FailureFrom(int index, Task task, Exception exception)
    {
        // A faulted task can hold more inner exceptions than the await surfaced, so the task is
        // the better source. A canceled task is not faulted, and only the caught exception says so.
        Exception failure = exception;
        if (task.IsFaulted)
        {
            AggregateException aggregate = task.Exception!;
            failure = aggregate.InnerExceptions.Count == 1
                ? aggregate.InnerExceptions[0]
                : aggregate;
        }

        EventHandlerModel handler = Handlers[index];
        return new EventHandlerFailure(
            handler.HandlerType,
            handler.DeclaredEventType,
            failure);
    }

    private Task<EventHandlerFailure?> StartForDeliveryAsync(
        int index,
        IServiceProvider? services,
        IEvent @event,
        CancellationToken cancellationToken)
    {
        Task task = Start(index, @event, services!, cancellationToken);
        return task.Status == TaskStatus.RanToCompletion
            ? NoFailureTask
            : CaptureFailureAsync(index, task);
    }

    private async Task<EventHandlerFailure?> CaptureFailureAsync(int index, Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
            return null;
        }
        catch (Exception exception)
        {
            return FailureFrom(index, task, exception);
        }
    }

    public abstract Task ExecuteAsync(
        IEvent @event,
        IServiceProvider services,
        CancellationToken cancellationToken);
}
