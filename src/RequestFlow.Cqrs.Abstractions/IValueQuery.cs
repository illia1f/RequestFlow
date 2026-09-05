namespace RequestFlow.Cqrs;

/// <summary>
/// Marks a ValueTask query that yields a response of type <typeparamref name="TResponse"/>.
/// </summary>
public interface IValueQuery<out TResponse> : IValueRequest<TResponse>
{ }
