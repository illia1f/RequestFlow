using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Sends a request to its single handler and returns the handler's response.
/// </summary>
public interface IValueRequestDispatcher
{
    /// <summary>
    /// Dispatches <paramref name="request"/> to its handler and returns the response.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="HandlerNotFoundException"/>
    /// <exception cref="ResponseTypeMismatchException"/>
    ValueTask<TResponse> SendAsync<TResponse>(
        IValueRequest<TResponse> request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a void <paramref name="request"/> to its handler.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="HandlerNotFoundException"/>
    /// <exception cref="ResponseTypeMismatchException"/>
    ValueTask SendAsync(
        IValueRequest request,
        CancellationToken cancellationToken = default);
}
