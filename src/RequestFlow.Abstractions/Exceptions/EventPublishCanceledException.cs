using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace RequestFlow;

/// <summary>
/// Thrown when an event publish strategy acknowledges cancellation.
/// </summary>
public sealed class EventPublishCanceledException : OperationCanceledException
{
    /// <exception cref="ArgumentException"/>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentOutOfRangeException"/>
    public EventPublishCanceledException(
        Type eventType,
        CancellationToken token,
        IReadOnlyList<EventHandlerFailure> failures,
        int skippedHandlerCount)
        : this(
            ValidateEventType(eventType),
            token,
            EventFailures.Copy(failures),
            ValidateSkippedHandlerCount(skippedHandlerCount))
    { }

    private EventPublishCanceledException(
        Type eventType,
        CancellationToken token,
        EventHandlerFailure[] failures,
        int skippedHandlerCount)
        : base(BuildMessage(eventType, failures, skippedHandlerCount), BuildInnerException(failures), token)
    {
        EventType = eventType;
        Failures = new ReadOnlyCollection<EventHandlerFailure>(failures);
        SkippedHandlerCount = skippedHandlerCount;
    }

    /// <summary>
    /// The type of event whose publish was canceled.
    /// </summary>
    public Type EventType { get; }

    /// <summary>
    /// The handler failures collected before cancellation was acknowledged.
    /// </summary>
    public IReadOnlyList<EventHandlerFailure> Failures { get; }

    /// <summary>
    /// The number of handler entries the strategy never started.
    /// </summary>
    public int SkippedHandlerCount { get; }

    private static string BuildMessage(
        Type eventType,
        EventHandlerFailure[] failures,
        int skippedHandlerCount)
    {
        if (failures.Length == 0)
        {
            return $"Publishing '{eventType.FullName}' was canceled before any handler failed; " +
                $"{skippedHandlerCount} handlers were skipped.";
        }

        return $"Publishing '{eventType.FullName}' was canceled after {failures.Length} handler failures; " +
            $"{skippedHandlerCount} handlers were skipped: {EventFailures.Describe(failures)}.";
    }

    private static Exception? BuildInnerException(EventHandlerFailure[] failures)
        => failures.Length == 0
            ? null
            : new AggregateException(EventFailures.ToExceptions(failures));

    private static Type ValidateEventType(Type eventType)
        => eventType ?? throw new ArgumentNullException(nameof(eventType));

    private static int ValidateSkippedHandlerCount(int skippedHandlerCount)
        => skippedHandlerCount >= 0
            ? skippedHandlerCount
            : throw new ArgumentOutOfRangeException(
                nameof(skippedHandlerCount),
                skippedHandlerCount,
                "A skipped handler count cannot be negative.");
}
