using RequestFlow;
using RequestFlow.Cqrs;

namespace Orders.Api.Rules;

/// <summary>
/// A request a command handler covers has to end in "Command", and one a query handler covers in "Query".
/// </summary>
public sealed class CommandNamingRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        List<RequestFlowValidationProblem> problems = [];
        foreach (RequestModel request in context.Model.Requests)
        {
            // A request with no handler is RF0102's to report, and one with several is RF0101's.
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

    // A void command handler records ICommandHandler<>, not ICommandHandler<,>, so matching only the
    // two-parameter shape would wave every void command through.
    private static string? SuffixFor(Type contract)
    {
        if (Implements(contract, typeof(ICommandHandler<,>)) || Implements(contract, typeof(ICommandHandler<>)))
            return "Command";

        return Implements(contract, typeof(IQueryHandler<,>)) ? "Query" : null;
    }

    // An application that gives its handlers a contract of their own has that one recorded instead,
    // and comparing for equality would drop every command from the check without saying so.
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
