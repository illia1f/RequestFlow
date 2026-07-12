namespace RequestFlow.Cqrs;

/// <summary>
/// Handles a single query type and produces its response.
/// </summary>
/// <typeparam name="TQuery">The query handled.</typeparam>
/// <typeparam name="TResponse">The response produced.</typeparam>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{ }
