using RequestFlow;

namespace Orders.Api.Validation;

/// <summary>
/// Runs a request's own checks and stops the chain when any of them fails. Program.cs registers it
/// behind a handler filter, so queries never enter it.
/// </summary>
public sealed class ValidationStage<TRequest, TResponse> : IRequestStage<TRequest, TResponse>
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
