using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Runs around the handler of every request this stage applies to. The implementing
/// class's generic constraints decide which requests those are.
/// </summary>
/// <typeparam name="TRequest">The request the stage wraps.</typeparam>
/// <typeparam name="TResponse">The response the wrapped handler produces.</typeparam>
public interface IRequestStage<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Wraps the rest of the chain for <paramref name="request"/>. Invoke
    /// <paramref name="next"/> to continue, or skip it to short-circuit.
    /// </summary>
    Task<TResponse> HandleAsync(TRequest request, Continuation<TResponse> next, CancellationToken cancellationToken);
}

/// <summary>
/// Runs around the handler of a request that returns nothing. Stages written against this
/// contract sit in the same chain, in the same registration order, as two-parameter ones.
/// </summary>
/// <typeparam name="TRequest">The void request the stage wraps.</typeparam>
public interface IRequestStage<in TRequest>
    where TRequest : IRequest<NoResult>
{
    /// <summary>
    /// Wraps the rest of the chain for <paramref name="request"/>. Invoke
    /// <paramref name="next"/> to continue, or skip it to short-circuit.
    /// </summary>
    Task HandleAsync(TRequest request, Continuation next, CancellationToken cancellationToken);
}
