using Orders.Modules.Orders.Validation;
using RequestFlow;
using RequestFlow.Cqrs;

namespace Orders.Modules.Orders.Rules;

/// <summary>
/// Reports commands whose frozen stage chain omits <see cref="ValidationStage{TRequest, TResponse}"/>.
/// </summary>
internal sealed class CommandValidationStageRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        List<RequestFlowValidationProblem> problems = [];
        foreach (RequestModel request in context.Model.Requests)
        {
            if (request.Handlers.Count != 1 || !IsCommand(request.RequestType))
                continue;

            if (HasValidationStage(request))
                continue;

            problems.Add(new RequestFlowValidationProblem(
                OrdersProblemCodes.CommandMissingValidationStage,
                $"Command '{request.RequestType.FullName}' has no validation stage in its chain; " +
                $"its handler '{request.Handlers[0].HandlerType.FullName}' has to implement " +
                $"{nameof(IOrdersCommandHandler)}.",
                request.RequestType));
        }

        return problems;
    }

    // Stages is the frozen chain, so ClosedType identifies what will run.
    private static bool HasValidationStage(RequestModel request)
    {
        foreach (ClosedStageModel stage in request.Stages)
        {
            if (stage.ClosedType.IsGenericType
                && stage.ClosedType.GetGenericTypeDefinition() == typeof(ValidationStage<,>))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCommand(Type requestType)
    {
        foreach (Type contract in requestType.GetInterfaces())
        {
            if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ICommand<>))
                return true;
        }

        return false;
    }
}
