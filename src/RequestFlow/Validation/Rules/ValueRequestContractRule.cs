using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Reports ambiguous ValueTask request contracts and handlers whose response does not match them.
/// </summary>
internal sealed class ValueRequestContractRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(
        RequestFlowValidationContext context)
    {
        List<Type> contracts = [];

        foreach (RequestModel request in context.Model.Requests)
        {
            contracts.Clear();
            foreach (Type iface in request.RequestType.GetInterfaces())
            {
                if (iface.IsGenericType
                    && iface.GetGenericTypeDefinition() == typeof(IValueRequest<>))
                {
                    contracts.Add(iface);
                }
            }

            if (contracts.Count > 1)
            {
                yield return new RequestFlowValidationProblem(
                    ProblemCodes.MultiContractValueRequest,
                    $"ValueTask request '{request.RequestType.FullName}' implements more than one " +
                    $"ValueTask request contract ({ContractFormatting.Format("IValueRequest", contracts)}); " +
                    "dispatch resolves one response type per request, so keep one contract and " +
                    "split the type if both responses are needed.",
                    request.RequestType);
            }

            Type? declaredResponse =
                ValueRequestContracts.GetSoleDeclaredResponse(request.RequestType);
            if (declaredResponse is null)
                continue;

            foreach (HandlerModel handler in request.Handlers)
            {
                if (!IsValueHandler(handler.ContractType))
                    continue;

                if (handler.ResponseType is null || handler.ResponseType == declaredResponse)
                    continue;

                yield return new RequestFlowValidationProblem(
                    ProblemCodes.ValueHandlerResponseMismatch,
                    $"ValueTask handler '{handler.HandlerType.FullName}' produces '{handler.ResponseType.FullName}' " +
                    $"for request '{request.RequestType.FullName}', which declares " +
                    $"IValueRequest<{declaredResponse.FullName}>; SendAsync infers the declared response type, so " +
                    "dispatching this request throws instead of returning. Give the handler the declared response type.",
                    handler.HandlerType);
            }
        }
    }

    private static bool IsValueHandler(Type contractType)
        => Implements(contractType, typeof(IValueRequestHandler<,>))
            || Implements(contractType, typeof(IValueRequestHandler<>));

    private static bool Implements(Type definition, Type contract)
    {
        if (definition == contract)
            return true;

        foreach (Type iface in definition.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == contract)
                return true;
        }

        return false;
    }
}
