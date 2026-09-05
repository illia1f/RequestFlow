using System;
using System.Collections.Generic;

namespace RequestFlow;

/// <summary>
/// Rejects multiple stream contracts and handler item types widened through <c>IStreamRequest&lt;TItem&gt;</c> covariance.
/// </summary>
internal sealed class StreamRequestContractRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        // Reused per request; GetInterfaces already returns each closed interface once.
        List<Type> streams = [];

        foreach (var request in context.Model.Requests)
        {
            streams.Clear();
            bool hasOtherRequestFamily = false;

            foreach (var iface in request.RequestType.GetInterfaces())
            {
                if (!iface.IsGenericType)
                    continue;

                Type definition = iface.GetGenericTypeDefinition();
                if (definition == typeof(IStreamRequest<>))
                    streams.Add(iface);
                else if (definition == typeof(IRequest<>)
                    || definition == typeof(IValueRequest<>))
                    hasOtherRequestFamily = true;
            }

            if (streams.Count == 0)
                continue;

            if (streams.Count > 1)
            {
                yield return new RequestFlowValidationProblem(
                    ProblemCodes.MultiContractStreamRequest,
                    $"Stream request '{request.RequestType.FullName}' implements more than one stream request contract " +
                    $"({ContractFormatting.Format("IStreamRequest", streams)}); dispatch resolves one item type per request, so keep one contract " +
                    "and split the type if both sequences are needed.",
                    request.RequestType);
            }

            // Only a sole, unambiguous contract names the item type a handler has to match.
            if (streams.Count == 1 && !hasOtherRequestFamily)
            {
                Type declaredItem = streams[0].GetGenericArguments()[0];

                foreach (var handler in request.Handlers)
                {
                    if (handler.ResponseType is null || handler.ResponseType == declaredItem)
                        continue;

                    yield return new RequestFlowValidationProblem(
                        ProblemCodes.StreamItemMismatch,
                        $"Stream handler '{handler.HandlerType.FullName}' produces '{handler.ResponseType.FullName}' " +
                        $"items for request '{request.RequestType.FullName}', which declares " +
                        $"IStreamRequest<{declaredItem.FullName}>; Stream infers the declared item type, so " +
                        "dispatching this request throws instead of streaming. Give the handler the declared item type.",
                        handler.HandlerType);
                }
            }
        }
    }
}
