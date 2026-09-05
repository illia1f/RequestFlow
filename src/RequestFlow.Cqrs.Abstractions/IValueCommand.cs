namespace RequestFlow.Cqrs;

/// <summary>
/// Marks a ValueTask command that yields a response of type <typeparamref name="TResponse"/>.
/// </summary>
public interface IValueCommand<out TResponse> : IValueRequest<TResponse>
{ }

/// <summary>
/// Marks a ValueTask command that returns nothing.
/// </summary>
public interface IValueCommand : IValueRequest, IValueCommand<NoResult>
{ }
