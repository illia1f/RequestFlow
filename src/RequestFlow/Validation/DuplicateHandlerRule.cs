using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reports every request type covered by more than one handler.
/// </summary>
/// <remarks>
/// The two registrations need not look alike. A scanned handler and a closing of a generic one
/// can land on the same request:
/// <code>
/// o.RegisterHandlersFromAssemblyContaining&lt;PlaceOrder&gt;()
///     .RegisterGenericHandler(typeof(AuditedHandler&lt;&gt;), typeof(PlaceOrder));
/// </code>
/// </remarks>
internal sealed class DuplicateHandlerRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        foreach (var request in context.Model.Requests)
        {
            if (request.Handlers.Count > 1)
            {
                yield return new RequestFlowValidationProblem(
                    ProblemCodes.DuplicateHandler,
                    $"Request '{request.RequestType.FullName}' has more than one handler; exactly one is required.",
                    request.RequestType);
            }
        }
    }
}
