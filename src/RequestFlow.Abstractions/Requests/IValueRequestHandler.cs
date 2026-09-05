using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Handles a single request type and produces its response.
/// </summary>
public interface IValueRequestHandler<in TRequest, TResponse>
    where TRequest : IValueRequest<TResponse>
{
    /// <summary>
    /// Handles <paramref name="request"/> and returns its response.
    /// </summary>
    ValueTask<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Handles a request that returns nothing.
/// </summary>
public interface IValueRequestHandler<in TRequest>
    where TRequest : IValueRequest<NoResult>
{
    /// <summary>
    /// Handles <paramref name="request"/>.
    /// </summary>
    ValueTask HandleAsync(TRequest request, CancellationToken cancellationToken);
}
