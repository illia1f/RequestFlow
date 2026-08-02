using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// The rest of the stage chain below one stage, ending at the request's handler. Invoke it
/// again after its task completes to run the rest of the chain again; invoking it while an
/// earlier call is still running throws <see cref="OverlappingNextCallException"/>.
/// </summary>
/// <remarks>
/// A repeated call walks the same stage instances: the chain resolves them once per
/// dispatch, not per call, so state a stage kept from the first pass is still there.
/// </remarks>
/// <typeparam name="TResponse">The response the chain produces.</typeparam>
public interface IContinuation<TResponse>
{
    /// <summary>
    /// Runs the rest of the chain.
    /// </summary>
    Task<TResponse> InvokeAsync();
}

/// <summary>
/// The void form of <see cref="IContinuation{TResponse}"/>, under the same rules.
/// </summary>
public interface IContinuation
{
    /// <summary>
    /// Runs the rest of the chain.
    /// </summary>
    Task InvokeAsync();
}
