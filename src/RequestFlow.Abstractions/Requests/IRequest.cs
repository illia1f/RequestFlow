namespace RequestFlow;

/// <summary>
/// Marks a message that is dispatched to exactly one handler and yields a
/// response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TResponse">The type produced by the handler.</typeparam>
public interface IRequest<out TResponse>
{ }

/// <summary>
/// Marks a request that returns nothing.
/// </summary>
public interface IRequest : IRequest<NoResult>
{ }
