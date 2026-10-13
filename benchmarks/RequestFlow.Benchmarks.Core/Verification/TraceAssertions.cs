namespace RequestFlow.Benchmarks.Core;

/// <summary>
/// Assertions over execution traces shared by the suite probes.
/// </summary>
public static class TraceAssertions
{
    /// <summary>
    /// Checks trace names, cancellation tokens and event completion order.
    /// </summary>
    /// <param name="ordered">Whether the names must follow <paramref name="expectedNames"/> in order, not just match them.</param>
    public static void VerifyRecords(
        string label,
        IReadOnlyList<ExecutionRecord> entries,
        string[] expectedNames,
        CancellationToken token,
        bool ordered = true)
    {
        string[] actual = entries.Select(entry => entry.Name).ToArray();
        bool namesMatch = ordered
            ? actual.SequenceEqual(expectedNames)
            : actual.Order(StringComparer.Ordinal).SequenceEqual(expectedNames.Order(StringComparer.Ordinal));

        Require(
            namesMatch,
            $"{label} did not execute the expected workload. Expected: {string.Join(", ", expectedNames)}. Actual: {string.Join(", ", actual)}.");
        Require(entries.All(entry => entry.Token == token), $"{label} passed the wrong cancellation token.");
        Require(HandlersCompleteAfterStart(actual), $"{label} recorded an event handler completion before its start.");
    }

    /// <summary>
    /// Throws if a verification condition fails.
    /// </summary>
    public static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static bool HandlersCompleteAfterStart(string[] names)
    {
        for (int index = 0; index < names.Length; index++)
        {
            string name = names[index];

            if (!name.StartsWith(TraceNames.EventHandlerPrefix, StringComparison.Ordinal) ||
                !name.EndsWith(TraceNames.CompletionSuffix, StringComparison.Ordinal))
                continue;

            int startIndex = Array.IndexOf(names, name[..^TraceNames.CompletionSuffix.Length]);

            if (startIndex < 0 || startIndex >= index)
                return false;
        }

        return true;
    }
}
