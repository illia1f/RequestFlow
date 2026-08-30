namespace Orders.Modules.Orders.Validation;

/// <summary>
/// The error response returned when request validation fails.
/// </summary>
public sealed record ValidationErrors(IReadOnlyList<string> Errors);
