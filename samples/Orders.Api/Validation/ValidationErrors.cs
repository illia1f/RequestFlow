namespace Orders.Api.Validation;

/// <summary>
/// The body a request that failed its own checks comes back with.
/// </summary>
public sealed record ValidationErrors(IReadOnlyList<string> Errors);
