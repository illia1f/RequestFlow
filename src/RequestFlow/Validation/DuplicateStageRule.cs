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
/// </remarks>
internal sealed class DuplicateStageRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        List<RequestFlowValidationProblem> problems = [];
        HashSet<Type> seenStages = [];

        // A stage registered three times is one problem, not two identical lines.
        HashSet<Type> reportedStages = [];
        foreach (var stage in context.Model.StageDeclarations)
        {
            if (!seenStages.Add(stage.StageType) && reportedStages.Add(stage.StageType))
            {
                problems.Add(new RequestFlowValidationProblem(
                    ProblemCodes.DuplicateStage,
                    $"Stage '{stage.StageType.FullName}' from assembly " +
                    $"'{stage.StageType.Assembly.GetName().Name}' is registered more than once; a stage belongs " +
                    "to a chain once, whatever each call filtered on. Remove the duplicate AddStage call.",
                    stage.StageType));
            }
        }

        return problems;
    }
}
