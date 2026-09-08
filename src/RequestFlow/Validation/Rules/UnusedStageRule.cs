using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reports stages that reach no request when validation of unused stages is enabled.
/// </summary>
/// <remarks>
/// Generic constraints can leave a stage with no matching request.
/// </remarks>
internal sealed class UnusedStageRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        // An unhandled request gets no stage chain, so a stage aimed at it looks unused here.
        bool anyUnhandled = false;

        HashSet<Type> applied = [];
        foreach (var request in context.Model.Requests)
        {
            if (request.Handlers.Count == 0)
                anyUnhandled = true;

            foreach (var closing in request.Stages)
                applied.Add(closing.DeclaredType);
        }

        HashSet<Type> reportedStages = [];
        foreach (var stage in context.Model.StageDeclarations)
        {
            if (!applied.Contains(stage.StageType) && reportedStages.Add(stage.StageType))
            {
                string message =
                    $"Stage '{stage.StageType.FullName}' from assembly '{stage.StageType.Assembly.GetName().Name}' applies to no registered request; widen its " +
                    "generic constraints, check its WhereHandlerImplements handler filter, scan the assembly holding the requests it targets, or drop DisallowUnusedStages.";

                if (anyUnhandled)
                {
                    message += context.AllUnhandledRequestsAllowed
                        ? " Some registered requests have no handler, which AllowAllUnhandledRequests permits; a stage reaching only those still counts as unused."
                        : " Some registered requests have no handler; a stage reaching only those counts as unused, so the missing handler may be the fix.";
                }

                yield return new RequestFlowValidationProblem(ProblemCodes.UnusedStage, message, stage.StageType);
            }
        }
    }
}
