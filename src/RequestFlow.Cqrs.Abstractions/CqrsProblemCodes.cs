namespace RequestFlow.Cqrs;

/// <summary>
/// Stable codes for the validation problems the CQRS package reports.
/// Documented in docs/validation-rules.md; never renumbered.
/// Match <see cref="RequestFlowValidationProblem.Code"/> against these instead of literal strings.
/// </summary>
public static class CqrsProblemCodes
{
    public const string CommandQuerySplit = "CQRS0001";
}
