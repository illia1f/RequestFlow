using RequestFlow;
using RequestFlow.Cqrs;

namespace Orders.Modules.Orders.Rules;

/// <summary>
/// Requires command request names to end in "Command" and query request names to end in "Query".
/// </summary>
internal sealed class CommandNamingRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        List<RequestFlowValidationProblem> problems = [];
        foreach (RequestModel request in context.Model.Requests)
        {
            if (request.Handlers.Count != 1)
                continue;

            HandlerModel handler = request.Handlers[0];
            string? suffix = SuffixFor(handler.ContractType);
            if (suffix is null || NameOf(request.RequestType).EndsWith(suffix, StringComparison.Ordinal))
                continue;

            problems.Add(new RequestFlowValidationProblem(
                OrdersProblemCodes.RequestSuffix,
                $"Request '{request.RequestType.FullName}' is handled by " +
                $"'{handler.HandlerType.FullName}', so its name has to end in '{suffix}'.",
                request.RequestType));
        }

        return problems;
    }

    // A closed generic request reads as "Audit`1" here, and the suffix sits in front of the backtick.
    private static string NameOf(Type requestType)
    {
        string name = requestType.Name;
        int arity = name.IndexOf('`');

        return arity < 0 ? name : name[..arity];
    }

    // Void command handlers use ICommandHandler<>, not ICommandHandler<,>.
    private static string? SuffixFor(Type contract)
    {
        if (Implements(contract, typeof(ICommandHandler<,>)) || Implements(contract, typeof(ICommandHandler<>)))
            return "Command";

        return Implements(contract, typeof(IQueryHandler<,>)) ? "Query" : null;
    }

    // Custom handler contracts appear here instead of the RequestFlow contract, so inspect inherited interfaces too.
    private static bool Implements(Type contract, Type coreContract)
    {
        if (contract == coreContract)
            return true;

        foreach (Type inherited in contract.GetInterfaces())
        {
            if (inherited.IsGenericType && inherited.GetGenericTypeDefinition() == coreContract)
                return true;
        }

        return false;
    }
}
