namespace Orders.Modules.Orders.Validation;

/// <summary>
/// Thrown by <see cref="ValidationStage{TRequest, TResponse}"/> when a request fails its own checks.
/// </summary>
public sealed class ValidationFailedException(IReadOnlyList<string> errors)
    : Exception($"The request failed validation: {string.Join("; ", errors)}")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
