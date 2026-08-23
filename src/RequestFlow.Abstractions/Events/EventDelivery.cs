using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

internal delegate Task<EventHandlerFailure?> EventDeliveryStart(
    int index,
    IServiceProvider? services,
    IEvent @event,
    CancellationToken cancellationToken);

/// <summary>
/// The handler entries available to one event publish.
/// </summary>
public readonly struct EventDelivery
{
    private const string NotBuiltMessage =
        "This delivery is the default value of its type, so there are no event entries to start. " +
        "A publish strategy is handed its delivery by the publisher; to build one in a test, call " +
        "EventDelivery.Over(event, subscriptions, start).";

    private const string NoServicesMessage =
        "This delivery has no service provider. Pass the services parameter to " +
        "EventDelivery.Over when the strategy under test reads Services.";

    private static readonly IReadOnlyList<EventHandlerFailure> NoFailures = [];

    private readonly IEvent _event;
    private readonly IServiceProvider? _services;
    private readonly EventSubscription[] _subscriptions;
    private readonly EventDeliveryStart _start;
    private readonly CancellationToken _cancellationToken;

    internal EventDelivery(
        IEvent @event,
        IServiceProvider? services,
        EventSubscription[] subscriptions,
        EventDeliveryStart start,
        CancellationToken cancellationToken)
    {
        _event = @event;
        _services = services;
        _subscriptions = subscriptions;
        _start = start;
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    /// The concrete type of the event being published.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public Type EventType => Event.GetType();

    /// <summary>
    /// The event being published.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public IEvent Event
    {
        get
        {
            EnsureBuilt();
            return _event;
        }
    }

    /// <summary>
    /// The provider used to resolve handlers for this publish.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The delivery was built with <see cref="Over"/> without its <c>services</c> parameter.
    /// </exception>
    public IServiceProvider Services
        => _services ?? throw new InvalidOperationException(NoServicesMessage);

    /// <summary>
    /// The number of handler entries in this delivery.
    /// </summary>
    public int Count => _subscriptions?.Length ?? 0;

    /// <summary>
    /// Gets the subscription at <paramref name="index"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"/>
    /// <exception cref="InvalidOperationException"/>
    public EventSubscription this[int index]
    {
        get
        {
            EnsureBuilt();

            if ((uint)index >= (uint)_subscriptions.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            return _subscriptions[index];
        }
    }

    /// <summary>
    /// Starts the entry at <paramref name="index"/> and returns its failure instead of throwing it.
    /// </summary>
    /// <param name="index">The zero-based entry index.</param>
    /// <param name="cancellationToken">
    /// The token the entry receives. Omit it, or pass <see cref="CancellationToken.None"/>, to use
    /// the token the strategy received for this publish.
    /// </param>
    /// <returns>
    /// The handler failure, or null when the entry succeeded. Discarding it drops the failure; use
    /// <see cref="RunAsync"/> to rethrow instead.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"/>
    /// <exception cref="InvalidOperationException"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<EventHandlerFailure?> StartAsync(
        int index, CancellationToken cancellationToken = default)
    {
        EnsureBuilt();

        if ((uint)index >= (uint)_subscriptions.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        return _start(
            index,
            _services,
            _event,
            cancellationToken == CancellationToken.None ? _cancellationToken : cancellationToken);
    }

    /// <summary>
    /// Runs the entry at <paramref name="index"/> and rethrows its handler exception on failure.
    /// </summary>
    /// <param name="index">The zero-based entry index.</param>
    /// <param name="cancellationToken">
    /// The token the entry receives. Omit it, or pass <see cref="CancellationToken.None"/>, to use
    /// the token the strategy received for this publish.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"/>
    /// <exception cref="InvalidOperationException"/>
    public async Task RunAsync(int index, CancellationToken cancellationToken = default)
    {
        EventHandlerFailure? failure = await StartAsync(index, cancellationToken)
            .ConfigureAwait(false);
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure.Exception).Throw();
    }

    /// <summary>
    /// Throws the standard publish exception when <paramref name="failures"/> contains entries.
    /// </summary>
    /// <param name="failures">The failures collected by the strategy.</param>
    /// <param name="skippedHandlerCount">The number of entries the strategy never started.</param>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="ArgumentOutOfRangeException"/>
    /// <exception cref="InvalidOperationException"/>
    public void ThrowIfAny(
        IReadOnlyList<EventHandlerFailure>? failures, int skippedHandlerCount = 0)
    {
        EnsureBuilt();

        if (failures is null || failures.Count == 0)
            return;

        throw new EventPublishException(
            EventType, failures, Count, skippedHandlerCount);
    }

    /// <summary>
    /// Creates the standard cancellation exception for this publish.
    /// </summary>
    /// <param name="failures">The failures collected before cancellation was acknowledged.</param>
    /// <param name="skippedHandlerCount">The number of entries the strategy never started.</param>
    /// <param name="cancellationToken">The publishing token the strategy acknowledged.</param>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="ArgumentOutOfRangeException"/>
    /// <exception cref="InvalidOperationException"/>
    public EventPublishCanceledException Canceled(
        IReadOnlyList<EventHandlerFailure>? failures,
        int skippedHandlerCount,
        CancellationToken cancellationToken)
        => new(
            EventType,
            cancellationToken,
            failures ?? NoFailures,
            skippedHandlerCount);

    /// <summary>
    /// Builds a delivery over test entries without requiring a container.
    /// </summary>
    /// <param name="event">The event being published.</param>
    /// <param name="subscriptions">The entries available to the strategy.</param>
    /// <param name="start">Starts one entry and returns its failure, or null when it succeeds.</param>
    /// <param name="services">The provider exposed through the delivery.</param>
    /// <param name="cancellationToken">The token used when an entry call names none.</param>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="ArgumentNullException"/>
    public static EventDelivery Over(
        IEvent @event,
        IReadOnlyList<EventSubscription> subscriptions,
        Func<int, CancellationToken, Task<EventHandlerFailure?>> start,
        IServiceProvider? services = null,
        CancellationToken cancellationToken = default)
    {
        if (@event is null)
            throw new ArgumentNullException(nameof(@event));
        if (subscriptions is null)
            throw new ArgumentNullException(nameof(subscriptions));
        if (start is null)
            throw new ArgumentNullException(nameof(start));

        var copiedSubscriptions = new EventSubscription[subscriptions.Count];
        for (int i = 0; i < copiedSubscriptions.Length; i++)
        {
            EventSubscription subscription = subscriptions[i];
            if (subscription.HandlerType is null || subscription.DeclaredEventType is null)
            {
                throw new ArgumentException(
                    "Subscriptions cannot contain the default EventSubscription value.",
                    nameof(subscriptions));
            }

            copiedSubscriptions[i] = subscription;
        }

        return new EventDelivery(
            @event,
            services,
            copiedSubscriptions,
            (index, _, _, token) => start(index, token),
            cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureBuilt()
    {
        if (_start is null)
            throw new InvalidOperationException(NotBuiltMessage);
    }
}
