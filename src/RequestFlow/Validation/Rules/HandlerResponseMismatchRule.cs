using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Rejects wider handler responses that compile through <c>IRequest&lt;TResponse&gt;</c> covariance.
/// Separate rules report requests with multiple response contracts or mixed request families.
/// </summary>
internal sealed class HandlerResponseMismatchRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        foreach (var request in context.Model.Requests)
        {
            if (request.Handlers.Count == 0)
                continue;

            Type? declared = RequestContracts.GetSoleDeclaredResponse(request.RequestType);
            if (declared is null)
                continue;

            foreach (var handler in request.Handlers)
            {
                // A void handler records no response and answers to IRequest<NoResult> instead.
                if (handler.ResponseType is null || handler.ResponseType == declared)
                    continue;

                yield return new RequestFlowValidationProblem(
                    ProblemCodes.HandlerResponseMismatch,
                    $"Handler '{handler.HandlerType.FullName}' produces '{handler.ResponseType.FullName}' " +
                    $"for request '{request.RequestType.FullName}', which declares " +
                    $"IRequest<{declared.FullName}>; SendAsync infers the declared response type, so " +
                    "dispatching this request throws instead of returning. Give the handler the declared " +
                    "response type.",
                    handler.HandlerType);
            }
        }
    }
}
