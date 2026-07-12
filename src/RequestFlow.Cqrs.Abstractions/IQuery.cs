namespace RequestFlow.Cqrs;

/// <summary>
/// Marks a request that reads state without mutating it and yields a response of type
/// <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TResponse">The type produced by the handler.</typeparam>
public interface IQuery<out TResponse> : IRequest<TResponse>
{ }
