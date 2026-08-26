using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reports a handler whose request type cannot be the exact runtime type used for dispatch.
/// </summary>
internal sealed class NonConcreteRequestRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        foreach (var request in context.Model.Requests)
        {
            // Interfaces count as abstract, so this one check covers both kinds.
            if (!request.RequestType.IsAbstract)
                continue;

#if NET462
            // RealProxy makes both shapes reachable through exact runtime-type lookup on .NET Framework.
            if (request.RequestType.IsInterface
                || typeof(System.MarshalByRefObject).IsAssignableFrom(request.RequestType))
                continue;
#endif

            string kind = request.RequestType.IsInterface ? "an interface" : "an abstract class";

            foreach (var handler in request.Handlers)
            {
                yield return new RequestFlowValidationProblem(
                    ProblemCodes.NonConcreteRequest,
                    $"Handler '{handler.HandlerType.FullName}' handles '{request.RequestType.FullName}', " +
                    $"which is {kind}; dispatch looks up the plan by exact runtime type, and this " +
                    "target cannot produce an instance of that type. Handle a concrete request type instead.",
                    handler.HandlerType);
            }
        }
    }
}
