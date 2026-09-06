using System;

namespace RequestFlow;

/// <summary>
/// A registered stage excluded from one request's pipeline.
/// </summary>
public sealed class ExcludedStageModel
{
    internal ExcludedStageModel(
        Type declaredType, Type? handlerFilter, StageExclusionReason reasonCode)
    {
        DeclaredType = declaredType;
        HandlerFilter = handlerFilter;
        ReasonCode = reasonCode;
    }

    /// <summary>
    /// The open or closed stage type supplied at registration.
    /// </summary>
    public Type DeclaredType { get; }

    /// <summary>
    /// The handler filter supplied at registration, or null when no filter was specified.
    /// </summary>
    public Type? HandlerFilter { get; }

    /// <summary>
    /// The first failed check: family, handler filter, generic constraints, or compatible contract.
    /// </summary>
    public StageExclusionReason ReasonCode { get; }
}
