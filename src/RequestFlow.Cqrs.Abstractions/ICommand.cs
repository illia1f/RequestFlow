namespace RequestFlow.Cqrs;

/// <summary>
/// Marks a request that changes state and yields a response of type
/// <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TResponse">The type produced by the handler.</typeparam>
public interface ICommand<out TResponse> : IRequest<TResponse>
{ }

/// <summary>
/// Marks a command that returns nothing.
/// </summary>
public interface ICommand : IRequest, ICommand<NoResult>
{ }
