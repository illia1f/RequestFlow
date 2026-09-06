namespace RequestFlow;

/// <summary>
/// The first failed check that excludes a registered stage from a request pipeline.
/// </summary>
/// <remarks>
/// Checks run in this order: family, handler filter, generic constraints, compatible contract.
/// </remarks>
public enum StageExclusionReason
{
    /// <summary>
    /// The stage's registration family differs from the handler's Task, ValueTask, or stream family.
    /// </summary>
    DifferentFamily = 1,

    /// <summary>
    /// The declared handler type is not assignable to the stage's configured handler filter.
    /// </summary>
    HandlerFilterNotMatched = 2,

    /// <summary>
    /// The type arguments used to close the open stage do not satisfy its generic constraints.
    /// </summary>
    GenericConstraintsNotSatisfied = 3,

    /// <summary>
    /// The closed stage does not implement a compatible stage contract for the request.
    /// </summary>
    ContractNotCompatible = 4
}
