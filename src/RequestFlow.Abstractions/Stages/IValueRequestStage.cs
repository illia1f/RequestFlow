using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Runs around the handler of every value request this stage applies to.
/// </summary>
public interface IValueRequestStage<in TRequest, TResponse>
    where TRequest : IValueRequest<TResponse>
{
    /// <summary>
    /// Wraps the rest of the chain for <paramref name="request"/>.
    /// </summary>
    ValueTask<TResponse> HandleAsync(
        TRequest request,
        ValueContinuation<TResponse> next,
        CancellationToken cancellationToken);
}

/// <summary>
/// Runs around the handler of a value request that returns nothing.
/// </summary>
public interface IValueRequestStage<in TRequest>
    where TRequest : IValueRequest<NoResult>
{
    /// <summary>
    /// Wraps the rest of the chain for <paramref name="request"/>.
    /// </summary>
    ValueTask HandleAsync(
        TRequest request,
        ValueContinuation next,
        CancellationToken cancellationToken);
}
