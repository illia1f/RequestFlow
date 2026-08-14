using System;

namespace RequestFlow;

/// <summary>
/// Thrown when a stream stage returns a null sequence from Handle, at the point the chain enters that stage's level.
/// </summary>
public sealed class StageNullStreamException(Type stageType)
    : NullStreamException(
        $"Stage '{stageType.FullName}' returned a null sequence from Handle; " +
        "return the sequence from next, or an empty sequence when short-circuiting.")
{
    /// <summary>
    /// The stage type that returned the null sequence.
    /// </summary>
    public Type StageType { get; } = stageType;
}
