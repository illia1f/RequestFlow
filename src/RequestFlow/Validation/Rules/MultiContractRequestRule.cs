using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reports every request type that implements more than one <c>IRequest&lt;TResponse&gt;</c>
/// contract. The dispatch map holds one plan per request type, so the extra contract could
/// only fail at dispatch, with a <c>ResponseTypeMismatchException</c>.
/// </summary>
/// <remarks>
/// A void request carries the <c>IRequest&lt;NoResult&gt;</c> contract, so this pair collides
/// the same way <c>IRequest&lt;string&gt;</c> next to <c>IRequest&lt;int&gt;</c> does:
/// <code>
/// public sealed record Purge : IRequest, IRequest&lt;int&gt;;
/// </code>
/// </remarks>
internal sealed class MultiContractRequestRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        // Reused per request; GetInterfaces already returns each closed interface once.
        List<Type> contracts = [];

        foreach (var request in context.Model.Requests)
        {
            contracts.Clear();
            foreach (var iface in request.RequestType.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IRequest<>))
                    contracts.Add(iface);
            }

            if (contracts.Count < 2)
                continue;

            yield return new RequestFlowValidationProblem(
                ProblemCodes.MultiContractRequest,
                $"Request '{request.RequestType.FullName}' implements more than one request contract ({ContractFormatting.Format("IRequest", contracts)}); " +
                $"dispatch resolves one response type per request, so keep one contract and split the type if both responses are needed.",
                request.RequestType);
        }
    }
}
