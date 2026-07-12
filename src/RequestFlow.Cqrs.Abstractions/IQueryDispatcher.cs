using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow.Cqrs;

/// <summary>
/// Sends a query to its single handler. Injection sites holding this interface
/// can send queries only, never commands.
/// </summary>
public interface IQueryDispatcher
{
    /// <summary>
    /// Dispatches <paramref name="query"/> to its handler and returns the response.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="HandlerNotFoundException"/>
    /// <exception cref="ResponseTypeMismatchException"/>
    Task<TResponse> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);
}
