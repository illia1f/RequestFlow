using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reports requests with no handler unless missing handlers are permitted.
/// </summary>
/// <remarks>
/// The scan picks up requests and handlers one assembly at a time, so a request whose handler
/// sits in an assembly nobody scanned is reported here.
/// </remarks>
internal sealed class UnhandledRequestRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        foreach (var request in context.Model.Requests)
        {
            if (request.Handlers.Count == 0 && !context.AllowsUnhandledRequest(request.RequestType))
            {
                yield return new RequestFlowValidationProblem(
                    ProblemCodes.UnhandledRequest,
                    $"Request '{request.RequestType.FullName}' has no handler.",
                    request.RequestType);
            }
        }
    }
}
