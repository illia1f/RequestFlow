using System;

namespace RequestFlow;

/// <summary>
/// Thrown by <see cref="IRequestDispatcher.SendAsync{TResponse}"/> when a stage invokes next
/// while the task from its earlier call is still running.
/// </summary>
public sealed class OverlappingNextCallException(Type stageType)
    : InvalidOperationException(
        $"Stage '{stageType.FullName}' called next while the task from its earlier call was still running. " +
        "Await that task before calling next again: each call runs the rest of the chain, so overlapping calls would run it twice at once.")
{
    /// <summary>
    /// The stage type that called next twice over.
    /// </summary>
    public Type StageType { get; } = stageType;
}
