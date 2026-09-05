using System;
using System.Collections.Generic;

namespace RequestFlow.Cqrs;

/// <summary>
/// Rejects requests classified as both commands and queries, including stream queries, regardless of response type.
/// </summary>
internal sealed class CommandQuerySplitRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        foreach (var request in context.Model.Requests)
        {
            bool isCommand = ImplementsDefinition(request.RequestType, typeof(ICommand<>))
                || ImplementsDefinition(request.RequestType, typeof(IValueCommand<>));
            bool isQuery = ImplementsDefinition(request.RequestType, typeof(IQuery<>))
                || ImplementsDefinition(request.RequestType, typeof(IValueQuery<>));
            bool isStreamQuery = ImplementsDefinition(request.RequestType, typeof(IStreamQuery<>));
            if (isCommand && (isQuery || isStreamQuery))
            {
                string querySide = isQuery ? "query" : "stream query";
                yield return new RequestFlowValidationProblem(
                    CqrsProblemCodes.CommandQuerySplit,
                    $"Request '{request.RequestType.FullName}' is classified as both a command and a {querySide}; pick one side of the split.",
                    request.RequestType);
            }
        }
    }

    private static bool ImplementsDefinition(Type requestType, Type definition)
    {
        foreach (var iface in requestType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == definition)
                return true;
        }

        return false;
    }
}
