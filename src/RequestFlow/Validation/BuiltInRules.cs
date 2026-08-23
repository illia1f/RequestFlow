using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// The rules RequestFlow runs on every freeze, in report order.
/// </summary>
internal static class BuiltInRules
{
    public static IEnumerable<IRequestFlowValidationRule> For(
        RequestFlowValidationContext context,
        IReadOnlyList<StageDeclaration> stageDeclarations,
        EventStrategyResolution eventStrategies)
    {
        // Built once, since three rules read it.
        StageDeclarationFacts facts = new(stageDeclarations);

        yield return new DuplicateHandlerRule();

        if (!context.UnhandledRequestsAllowed)
            yield return new UnhandledRequestRule();

        yield return new DuplicateStageRule(facts);
        yield return new AliasedStageRule();

        if (context.UnusedStagesDisallowed)
            yield return new UnusedStageRule();

        yield return new MultiContractRequestRule();
        yield return new StreamRequestContractRule();

        // After the stream contract rule, so an RF0110 from the same freeze precedes this RF0109.
        // Report order is ascending by code apart from that pair.
        yield return new ContractConflictRule(
            MessageContract.Request,
            MessageContract.StreamRequest,
            ProblemCodes.RequestAndStreamRequest);
        yield return new StreamStageItemMismatchRule(facts);

        yield return new HandlerResponseMismatchRule();
        yield return new StageResponseMismatchRule(facts);

        if (!context.UnhandledEventsAllowed)
            yield return new UnhandledEventRule();

        if (context.UnusedEventHandlersDisallowed)
            yield return new UnusedEventSubscriptionRule();

        yield return new ContractConflictRule(
            MessageContract.Request,
            MessageContract.Event,
            ProblemCodes.RequestAndEvent);
        yield return new ContractConflictRule(
            MessageContract.StreamRequest,
            MessageContract.Event,
            ProblemCodes.StreamRequestAndEvent);

        yield return new StageEventHandlerLifetimeRule();
        yield return new EventStrategyRule(eventStrategies);
    }
}
