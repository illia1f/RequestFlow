namespace RequestFlow.Cqrs;

/// <summary>
/// Handles a single ValueTask command type and produces its response.
/// </summary>
public interface IValueCommandHandler<in TCommand, TResponse>
    : IValueRequestHandler<TCommand, TResponse>
    where TCommand : IValueCommand<TResponse>
{ }

/// <summary>
/// Handles a ValueTask command that returns nothing.
/// </summary>
public interface IValueCommandHandler<in TCommand>
    : IValueRequestHandler<TCommand>
    where TCommand : IValueCommand
{ }
