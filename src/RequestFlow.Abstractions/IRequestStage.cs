using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Runs the rest of the stage chain, ending at the request's handler. Call it again after
/// its task completes to run the rest of the chain again; calling it while an earlier call
/// is still running throws <see cref="System.InvalidOperationException"/>.
/// </summary>
/// <remarks>
/// A repeated call walks the same stage instances: the chain resolves them once per
/// dispatch, not per call, so state a stage kept from the first pass is still there.
/// </remarks>
/// <typeparam name="TResponse">The response the chain produces.</typeparam>
public delegate Task<TResponse> StageDelegate<TResponse>();

/// <summary>
/// The void form of <see cref="StageDelegate{TResponse}"/>, under the same rules.
/// </summary>
public delegate Task StageDelegate();

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
    /// Wraps the rest of the chain for <paramref name="request"/>. Call
    /// <paramref name="next"/> to continue, or skip it to short-circuit.
    /// </summary>
    Task<TResponse> HandleAsync(TRequest request, StageDelegate<TResponse> next, CancellationToken cancellationToken);
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
    /// Wraps the rest of the chain for <paramref name="request"/>. Call
    /// <paramref name="next"/> to continue, or skip it to short-circuit.
    /// </summary>
    Task HandleAsync(TRequest request, StageDelegate next, CancellationToken cancellationToken);
}
