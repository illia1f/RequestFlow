using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Rejects repeated stage types regardless of handler filters.
/// </summary>
/// <remarks>
/// <paramref name="facts"/> preserves registration call order across families.
/// When null, diagnostics use the model's recorded contracts.
/// </remarks>
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
                string remedy = _facts.BuildDuplicateRemovalAdvice(
                    stage.StageType,
                    stage.ContractType);

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
