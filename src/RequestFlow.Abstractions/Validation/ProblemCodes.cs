namespace RequestFlow;

/// <summary>
/// Stable codes for every built-in validation problem. Documented in docs/validation-rules.md;
/// never renumbered. Match <see cref="RequestFlowValidationProblem.Code"/> against these
/// instead of literal strings.
/// </summary>
public static class ProblemCodes
{
    public const string HandlerNotOpenGeneric = "RF0001";
    public const string HandlerAbstract = "RF0002";
    public const string HandlerWrongArity = "RF0003";
    public const string HandlerMissingContract = "RF0004";
    public const string NoClosingTypes = "RF0005";
    public const string ClosingTypeNotClosed = "RF0006";
    public const string ClosingViolatesConstraints = "RF0007";
    public const string StageIsInterface = "RF0008";
    public const string StageAbstract = "RF0009";
    public const string StagePartiallyClosed = "RF0010";
    public const string StageMissingContract = "RF0011";
    public const string StageParametersMisused = "RF0012";
    public const string DuplicateHandler = "RF0101";
    public const string UnhandledRequest = "RF0102";
    public const string DuplicateStage = "RF0103";
    public const string AliasedStage = "RF0104";
    public const string UnusedStage = "RF0105";
    public const string MultiContractRequest = "RF0106";
    public const string RuleFailed = "RF0107";
}
