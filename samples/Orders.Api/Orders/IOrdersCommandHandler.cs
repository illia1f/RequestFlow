namespace Orders.Api.Orders;

/// <summary>
/// Every command handler in the application implements this, and the validation stage filters on it.
/// </summary>
/// <remarks>
/// A stage filter takes one contract as a type argument and an open generic definition is not legal
/// there, so a marker is what covers both command handler shapes at once. Forgetting it on a new
/// handler still compiles and still dispatches, which is what CommandValidatedRule catches.
/// </remarks>
public interface IOrdersCommandHandler
{ }
