namespace RequestFlow.Benchmarks.Core;

/// <summary>
/// The trace record names that workload components write and the probe checks.
/// </summary>
public static class TraceNames
{
    /// <summary>
    /// A request or stream handler.
    /// </summary>
    public const string Handler = "handler";

    /// <summary>
    /// Prefixes the one-based number of a stage.
    /// </summary>
    public const string StagePrefix = "stage:";

    /// <summary>
    /// Prefixes the one-based number of an event handler.
    /// </summary>
    public const string EventHandlerPrefix = "handler:";

    /// <summary>
    /// Follows an event handler name once its awaitable completes.
    /// </summary>
    public const string CompletionSuffix = ":complete";

    /// <summary>
    /// Prefixes the zero-based index of a stream item.
    /// </summary>
    public const string ItemPrefix = "item:";

    /// <summary>
    /// A stream that yielded every item.
    /// </summary>
    public const string StreamComplete = "stream:complete";

    /// <summary>
    /// A stream whose enumerator was disposed.
    /// </summary>
    public const string StreamDispose = "stream:dispose";

    public static bool IsEventHandlerStart(string name)
        => name.StartsWith(EventHandlerPrefix, StringComparison.Ordinal) &&
            !name.EndsWith(CompletionSuffix, StringComparison.Ordinal);
}
