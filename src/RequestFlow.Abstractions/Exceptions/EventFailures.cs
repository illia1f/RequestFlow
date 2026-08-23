using System;
using System.Collections.Generic;

namespace RequestFlow;

internal static class EventFailures
{
    /// <exception cref="ArgumentException"/>
    /// <exception cref="ArgumentNullException"/>
    public static EventHandlerFailure[] Copy(IReadOnlyList<EventHandlerFailure> failures)
    {
        if (failures is null)
            throw new ArgumentNullException(nameof(failures));

        int count = failures.Count;
        var copiedFailures = new EventHandlerFailure[count];

        for (var index = 0; index < count; index++)
        {
            copiedFailures[index] = failures[index]
                ?? throw new ArgumentException("Failures cannot contain null entries.", nameof(failures));
        }

        return copiedFailures;
    }

    /// <summary>
    /// Joins each failure into one comma-separated list of handler type and declared event type.
    /// </summary>
    public static string Describe(EventHandlerFailure[] failures)
    {
        var details = new string[failures.Length];

        for (var index = 0; index < failures.Length; index++)
        {
            EventHandlerFailure failure = failures[index];
            details[index] = $"'{failure.HandlerType.FullName}' declared for '{failure.DeclaredEventType.FullName}'";
        }

        return string.Join(", ", details);
    }

    public static Exception[] ToExceptions(EventHandlerFailure[] failures)
    {
        var exceptions = new Exception[failures.Length];

        for (var index = 0; index < failures.Length; index++)
            exceptions[index] = failures[index].Exception;

        return exceptions;
    }
}
