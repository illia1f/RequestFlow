namespace RequestFlow;

/// <summary>
/// Marks a request with one handler that returns <typeparamref name="TResponse"/>.
/// </summary>
public interface IValueRequest<out TResponse>
{ }

/// <summary>
/// Marks a request that returns nothing.
/// </summary>
public interface IValueRequest : IValueRequest<NoResult>
{ }
