using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow.Cqrs;

/// <summary>
/// Sends ValueTask queries to their handlers.
/// </summary>
public interface IValueQueryDispatcher
{
    /// <summary>
    /// Dispatches <paramref name="query"/> to its handler and returns the response.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="HandlerNotFoundException"/>
    /// <exception cref="ResponseTypeMismatchException"/>
    ValueTask<TResponse> SendAsync<TResponse>(
        IValueQuery<TResponse> query,
        CancellationToken cancellationToken = default);
}
