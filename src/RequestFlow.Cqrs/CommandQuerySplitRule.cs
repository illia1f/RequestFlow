using System;
using System.Collections.Generic;

namespace RequestFlow.Cqrs;

/// <summary>
/// Reports every request classified as both a command and a query. Checks contract
/// assignability, not response shapes, so it also catches ICommand&lt;A&gt; next to
/// IQuery&lt;B&gt;.
/// </summary>
internal sealed class CommandQuerySplitRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        List<RequestFlowValidationProblem> problems = [];
        foreach (var request in context.Model.Requests)
        {
            if (ImplementsDefinition(request.RequestType, typeof(ICommand<>))
                && ImplementsDefinition(request.RequestType, typeof(IQuery<>)))
            {
                problems.Add(new RequestFlowValidationProblem(
                    CqrsProblemCodes.CommandQuerySplit,
                    $"Request '{request.RequestType.FullName}' is classified as both a command and a query; pick one side of the split.",
                    request.RequestType));
            }
        }

        return problems;
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
