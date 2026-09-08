using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reports every stage that reached no request. The freeze runs this rule only when the
/// application called <c>DisallowUnusedStages</c>; whether <c>AllowUnhandledRequests</c> was
/// called too decides how the message reads.
/// </summary>
/// <remarks>
/// Constraints are how a stage picks its requests, so one nothing satisfies closes for nothing:
/// <code>
/// class AuditStage&lt;TRequest, TResponse&gt; : IRequestStage&lt;TRequest, TResponse&gt;
///     where TRequest : IAudited, IRequest&lt;TResponse&gt;
/// </code>
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
                        ? " Some registered requests have no handler, which AllowUnhandledRequests permits; a stage reaching only those still counts as unused."
                        : " Some registered requests have no handler; a stage reaching only those counts as unused, so the missing handler may be the fix.";
                }

                yield return new RequestFlowValidationProblem(ProblemCodes.UnusedStage, message, stage.StageType);
            }
        }
    }
}
