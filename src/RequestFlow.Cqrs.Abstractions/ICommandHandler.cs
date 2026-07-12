namespace RequestFlow.Cqrs;

/// <summary>
/// Handles a single command type and produces its response.
/// </summary>
/// <typeparam name="TCommand">The command handled.</typeparam>
/// <typeparam name="TResponse">The response produced.</typeparam>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{ }

/// <summary>
/// Handles a command that returns nothing.
/// </summary>
/// <typeparam name="TCommand">The void command handled.</typeparam>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand>
    where TCommand : ICommand
{ }
