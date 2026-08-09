using Orders.Api.Orders;
using Orders.Api.Validation;
using RequestFlow;
using RequestFlow.Cqrs;

namespace Orders.Api.Rules;

/// <summary>
/// Every command has to run through <see cref="ValidationStage{TRequest, TResponse}"/>. A handler
/// that leaves off <see cref="IOrdersCommandHandler"/> falls out of the stage's filter.
/// </summary>
public sealed class CommandValidatedRule : IRequestFlowValidationRule
{
    public IEnumerable<RequestFlowValidationProblem> Validate(RequestFlowValidationContext context)
    {
        List<RequestFlowValidationProblem> problems = [];
        foreach (RequestModel request in context.Model.Requests)
        {
            if (request.Handlers.Count != 1 || !IsCommand(request.RequestType))
                continue;

            if (Validates(request))
                continue;

            problems.Add(new RequestFlowValidationProblem(
                OrdersProblemCodes.CommandNotValidated,
                $"Command '{request.RequestType.FullName}' has no validation stage in its chain; " +
                $"its handler '{request.Handlers[0].HandlerType.FullName}' has to implement " +
                $"{nameof(IOrdersCommandHandler)}.",
                request.RequestType));
        }

        return problems;
    }

    // Stages is the chain as the freeze closed it, so this reads what will actually run. ClosedType
    // is the same either way, while DeclaredType is whatever shape AddStage was handed.
    private static bool Validates(RequestModel request)
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
