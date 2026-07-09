using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Sends a request to its single handler and returns the handler's response.
/// </summary>
public interface IRequestDispatcher
{
    /// <summary>
    /// Dispatches <paramref name="request"/> to its handler and returns the response.
    /// </summary>
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a void <paramref name="request"/> to its handler.
    /// </summary>
    Task SendAsync(IRequest request, CancellationToken cancellationToken = default);
}
