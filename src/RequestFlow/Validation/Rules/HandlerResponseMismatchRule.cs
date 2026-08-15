using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reports a handler whose response type is not the one its request declares. The dispatch map holds
/// one plan per request type, closed over the handler's response, so the pair would only fail
/// once somebody dispatched it.
/// </summary>
/// <remarks>
/// The task twin of the item check in <see cref="StreamRequestContractRule"/>. It exists because
/// <c>IRequest&lt;TResponse&gt;</c> is covariant: a handler declaring a wider response satisfies its
/// own constraint and compiles, but closes a plan no inferred <c>SendAsync</c> call can hit.
/// <para>
/// A request carrying two response contracts is left to <see cref="MultiContractRequestRule"/>, and
/// one carrying a stream contract as well to <see cref="StreamRequestContractRule"/>, since neither
/// names the single response a handler has to match.
/// </para>
/// </remarks>
internal sealed class HandlerResponseMismatchRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        foreach (var request in context.Model.Requests)
        {
            if (request.Handlers.Count == 0)
                continue;

            Type? declared = RequestContracts.SoleDeclaredResponse(request.RequestType);
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
