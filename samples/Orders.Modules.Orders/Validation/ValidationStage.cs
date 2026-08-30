using RequestFlow;

namespace Orders.Modules.Orders.Validation;

/// <summary>
/// Runs a request's checks before its handler and stops the chain when any check fails.
/// </summary>
internal sealed class ValidationStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public Task<TResponse> HandleAsync(
        TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is IValidatableRequest validatable)
        {
            List<string> errors = [.. validatable.Validate()];
            if (errors.Count > 0)
                throw new ValidationFailedException(errors);
        }

        return next.InvokeAsync(cancellationToken);
    }
}
