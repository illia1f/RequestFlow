using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reports every request type no handler covers. The freeze skips this rule when
/// <c>AllowUnhandledRequests</c> was called.
/// </summary>
/// <remarks>
/// The scan picks up requests and handlers one assembly at a time, so a request whose handler
/// sits in an assembly nobody scanned is reported here.
/// </remarks>
internal sealed class UnhandledRequestRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        List<RequestFlowValidationProblem> problems = [];
        foreach (var request in context.Model.Requests)
        {
            if (request.Handlers.Count == 0)
            {
                problems.Add(new RequestFlowValidationProblem(
                    ProblemCodes.UnhandledRequest,
                    $"Request '{request.RequestType.FullName}' has no handler.",
                    request.RequestType));
            }
        }

        return problems;
    }
}
