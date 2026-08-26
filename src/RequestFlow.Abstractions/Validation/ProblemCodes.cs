namespace RequestFlow;

/// <summary>
/// Stable codes for every built-in validation problem. Documented in docs/validation-rules.md;
/// never renumbered. Match <see cref="RequestFlowValidationProblem.Code"/> against these instead of literal strings.
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
    public const string EventStrategyIsInterface = "RF0013";
    public const string EventStrategyAbstract = "RF0014";
    public const string EventHandlerIsInterface = "RF0015";
    public const string EventHandlerAbstract = "RF0016";
    public const string EventHandlerMissingContract = "RF0017";
    public const string ManualHandlerIsInterface = "RF0018";
    public const string ManualHandlerAbstract = "RF0019";
    public const string ManualHandlerMissingContract = "RF0020";
    public const string DuplicateHandler = "RF0101";
    public const string UnhandledRequest = "RF0102";
    public const string DuplicateStage = "RF0103";
    public const string AliasedStage = "RF0104";
    public const string UnusedStage = "RF0105";
    public const string MultiContractRequest = "RF0106";
    public const string RuleFailed = "RF0107";
    public const string MultiContractStreamRequest = "RF0108";
    public const string RequestAndStreamRequest = "RF0109";
    public const string StreamItemMismatch = "RF0110";
    public const string StreamStageItemMismatch = "RF0111";
    public const string HandlerResponseMismatch = "RF0112";
    public const string StageResponseMismatch = "RF0113";
    public const string UnhandledEvent = "RF0114";
    public const string UnusedEventSubscription = "RF0115";
    public const string RequestAndEvent = "RF0116";
    public const string StreamRequestAndEvent = "RF0117";
    public const string StageEventHandlerLifetime = "RF0118";
    public const string ConflictingEventStrategies = "RF0119";
    public const string AmbiguousEventStrategy = "RF0120";
    public const string EventStrategyLifetime = "RF0121";
    public const string UnusedEventStrategy = "RF0122";
    public const string EventStrategyRoleLifetime = "RF0123";
    public const string NonConcreteRequest = "RF0124";
}
