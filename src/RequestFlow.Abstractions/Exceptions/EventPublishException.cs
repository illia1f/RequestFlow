using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RequestFlow;

/// <summary>
/// Thrown when one or more handlers fail while an event is published.
/// </summary>
public sealed class EventPublishException : AggregateException
{
    // A caller that supplies only failures has no total to report, so the message leaves it out.
    private const int UnknownHandlerCount = -1;

    /// <exception cref="ArgumentException"/>
    /// <exception cref="ArgumentNullException"/>
    public EventPublishException(Type eventType, IReadOnlyList<EventHandlerFailure> failures)
        : this(eventType, failures, UnknownHandlerCount, skippedHandlerCount: 0)
    { }

    internal EventPublishException(Type eventType, IReadOnlyList<EventHandlerFailure> failures, int handlerCount)
        : this(eventType, failures, handlerCount, skippedHandlerCount: 0)
    { }

    internal EventPublishException(
        Type eventType,
        IReadOnlyList<EventHandlerFailure> failures,
        int handlerCount,
        int skippedHandlerCount)
        : this(
            ValidateEventType(eventType),
            CopyFailures(failures),
            handlerCount,
            ValidateSkippedHandlerCount(skippedHandlerCount))
    { }

    private EventPublishException(
        Type eventType,
        EventHandlerFailure[] failures,
        int handlerCount,
        int skippedHandlerCount)
        : base(
            BuildMessage(eventType, failures, handlerCount, skippedHandlerCount),
            EventFailures.ToExceptions(failures))
    {
        EventType = eventType;
        Failures = new ReadOnlyCollection<EventHandlerFailure>(failures);
        SkippedHandlerCount = skippedHandlerCount;
    }

    /// <summary>
    /// The type of event that was published.
    /// </summary>
    public Type EventType { get; }

    /// <summary>
    /// The handler failures collected while publishing the event.
    /// </summary>
    public IReadOnlyList<EventHandlerFailure> Failures { get; }

    /// <summary>
    /// The number of handler entries the strategy never started.
    /// </summary>
    public int SkippedHandlerCount { get; }

    private static string BuildMessage(
        Type eventType,
        EventHandlerFailure[] failures,
        int handlerCount,
        int skippedHandlerCount)
    {
        string counted = handlerCount == UnknownHandlerCount
            ? $"{failures.Length} handlers"
            : $"{failures.Length} of {handlerCount} handlers";

        if (skippedHandlerCount > 0)
        {
            string skipped = skippedHandlerCount == 1
                ? "1 handler was skipped"
                : $"{skippedHandlerCount} handlers were skipped";

            return $"Publishing '{eventType.FullName}' failed in {counted}; {skipped}: " +
                EventFailures.Describe(failures) + ".";
        }

        return $"Publishing '{eventType.FullName}' failed in {counted}: " +
            EventFailures.Describe(failures) + ".";
    }

    private static Type ValidateEventType(Type eventType)
        => eventType ?? throw new ArgumentNullException(nameof(eventType));

    private static EventHandlerFailure[] CopyFailures(IReadOnlyList<EventHandlerFailure> failures)
    {
        EventHandlerFailure[] copiedFailures = EventFailures.Copy(failures);
        if (copiedFailures.Length == 0)
            throw new ArgumentException("A publish needs at least one failure.", nameof(failures));

        return copiedFailures;
    }

    private static int ValidateSkippedHandlerCount(int skippedHandlerCount)
        => skippedHandlerCount >= 0
            ? skippedHandlerCount
            : throw new ArgumentOutOfRangeException(
                nameof(skippedHandlerCount),
                skippedHandlerCount,
                "A skipped handler count cannot be negative.");
}
