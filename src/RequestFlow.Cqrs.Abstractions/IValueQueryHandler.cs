namespace RequestFlow.Cqrs;

/// <summary>
/// Handles a single ValueTask query type and produces its response.
/// </summary>
public interface IValueQueryHandler<in TQuery, TResponse>
    : IValueRequestHandler<TQuery, TResponse>
    where TQuery : IValueQuery<TResponse>
{ }
