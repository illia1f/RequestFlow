using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Handles a single request type and produces its response. Exactly one handler is
/// registered per closed request/response pair; the dispatcher resolves and invokes it.
/// </summary>
/// <typeparam name="TRequest">The request handled.</typeparam>
/// <typeparam name="TResponse">The response produced.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles <paramref name="request"/> and returns its response.
    /// </summary>
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Handles a request that returns nothing.
/// </summary>
/// <typeparam name="TRequest">The void request handled.</typeparam>
public interface IRequestHandler<in TRequest>
    where TRequest : IRequest<NoResult>
{
    /// <summary>
    /// Handles <paramref name="request"/>.
    /// </summary>
    Task HandleAsync(TRequest request, CancellationToken cancellationToken);
}
