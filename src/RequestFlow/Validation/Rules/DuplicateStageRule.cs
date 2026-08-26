using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reports every stage type registered more than once. A stage belongs to a chain once,
/// whatever each call filtered on.
/// </summary>
/// <remarks>
/// A handler filter does not make the second call a different stage:
/// <code>
/// o.AddStage(typeof(LoggingStage&lt;,&gt;), s => s.WhereHandlerImplements&lt;IAudited&gt;())
///     .AddStage(typeof(LoggingStage&lt;,&gt;));
/// </code>
/// The message names the call the application made, which <paramref name="facts"/> holds. A stage
/// implementing a contract from both families reads as a stream stage otherwise. A stage type
/// registered once per family repeated neither call, so that message names both.
/// </remarks>
/// <param name="facts">
/// What the calls that registered these stages named. Null for a model built by hand, where the
/// recorded contract answers instead.
/// </param>
internal sealed class DuplicateStageRule(StageDeclarationFacts? facts = null) : IRequestFlowValidationRule
{
    private readonly StageDeclarationFacts _facts = facts ?? StageDeclarationFacts.None;

    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        HashSet<Type> seenStages = [];

        // A stage registered three times is one problem, not two identical lines.
        HashSet<Type> reportedStages = [];
        foreach (var stage in context.Model.StageDeclarations)
        {
            if (!seenStages.Add(stage.StageType) && reportedStages.Add(stage.StageType))
            {
                string remedy = _facts.RegisteredInBothFamilies(stage.StageType)
                    ? $"Remove the {StageFamily.Request.CallName} call or the {StageFamily.Stream.CallName} call."
                    : $"Remove the duplicate {_facts.GetFamily(stage.StageType, stage.ContractType).CallName} call.";

                yield return new RequestFlowValidationProblem(
                    ProblemCodes.DuplicateStage,
                    $"Stage '{stage.StageType.FullName}' from assembly " +
                    $"'{stage.StageType.Assembly.GetName().Name}' is registered more than once; a stage belongs " +
                    $"to a chain once, whatever each call filtered on. {remedy}",
                    stage.StageType);
            }
        }
    }
}
