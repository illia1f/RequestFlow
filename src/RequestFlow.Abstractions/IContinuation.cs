using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// The rest of the stage chain below one stage, ending at the request's handler. Invoke it again
/// to run that chain again, either after the first call finishes or while it is still running.
/// </summary>
/// <remarks>
/// Each call enters the levels below on its own and keeps the token it was given, so the library
/// holds no state two overlapping calls can collide over. What they share is whatever the container
/// hands to both: every entry resolves its stage, so a transient stage below is built again per
/// call, while a scoped or singleton one runs inside both calls at once and has to be thread safe.
/// <para>
/// Resolving that level happens before there is a task to hand back, so <see cref="InvokeAsync"/>
/// can throw instead of returning one. A stage holding a call it has not awaited owns that call,
/// and has to observe it when a later call or walk fails.
/// </para>
/// </remarks>
/// <typeparam name="TResponse">The response the chain produces.</typeparam>
public interface IContinuation<TResponse>
{
    /// <summary>
    /// Runs the rest of the chain.
    /// </summary>
    /// <param name="cancellationToken">
    /// The token every level below this stage runs under, the handler included. Omit it, or
    /// pass <see cref="CancellationToken.None"/>, to continue under the token this stage
    /// received.
    /// </param>
    Task<TResponse> InvokeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// The void form of <see cref="IContinuation{TResponse}"/>, under the same rules.
/// </summary>
public interface IContinuation
{
    /// <summary>
    /// Runs the rest of the chain.
    /// </summary>
    /// <param name="cancellationToken">
    /// The token every level below this stage runs under, the handler included. Omit it, or
    /// pass <see cref="CancellationToken.None"/>, to continue under the token this stage
    /// received.
    /// </param>
    Task InvokeAsync(CancellationToken cancellationToken = default);
}
