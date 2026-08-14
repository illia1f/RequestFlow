using System;

namespace RequestFlow;

/// <summary>
/// Thrown by <see cref="IRequestDispatcher.SendAsync{TResponse}"/> when a stage returns a null task from HandleAsync.
/// </summary>
public sealed class StageNullTaskException(Type stageType)
    : NullTaskException(
        $"Stage '{stageType.FullName}' returned a null task from HandleAsync; " +
        "return the task from next, or a completed task when short-circuiting.")
{
    /// <summary>
    /// The stage type that returned the null task.
    /// </summary>
    public Type StageType { get; } = stageType;
}
