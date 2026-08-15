using System.Collections.Generic;
using System.Threading;

namespace RequestFlow;

/// <summary>
/// Runs around the handler of every stream request this stage applies to. The implementing
/// class's generic constraints decide which requests those are.
/// </summary>
/// <typeparam name="TRequest">The stream request the stage wraps.</typeparam>
/// <typeparam name="TItem">The type each element of the sequence has.</typeparam>
public interface IStreamRequestStage<in TRequest, TItem>
    where TRequest : IStreamRequest<TItem>
{
    /// <summary>
    /// Wraps the rest of the chain for <paramref name="request"/>. Invoke <paramref name="next"/>
    /// to continue, or skip it to short-circuit. The items may be observed, filtered, projected, or cut short.
    /// </summary>
    /// <remarks>
    /// An implementation written as an async iterator decorates its token parameter with
    /// <c>[EnumeratorCancellation]</c>, under the same
    /// rule as <see cref="IStreamRequestHandler{TRequest, TItem}.Handle"/>.
    /// </remarks>
    IAsyncEnumerable<TItem> Handle(
        TRequest request, StreamContinuation<TItem> next, CancellationToken cancellationToken);
}
