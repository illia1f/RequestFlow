using System.Collections.Generic;
using System.Threading;

namespace RequestFlow;

/// <summary>
/// Sends a stream request to its handler and hands back the sequence.
/// </summary>
public interface IStreamDispatcher
{
    /// <summary>
    /// Resolves the plan for <paramref name="request"/> and returns the sequence its handler produces, wrapped in whatever stream stages apply.
    /// </summary>
    /// <param name="request">The request to stream.</param>
    /// <param name="cancellationToken">
    /// The token the walk runs under. A token passed to <c>WithCancellation</c> on the returned
    /// sequence is honoured as well, and either one cancels every level that runs under the join.
    /// A stage that hands <see cref="StreamContinuation{TItem}.Invoke"/> a token of its own
    /// replaces both for the levels below it, which this call does not undo.
    /// </param>
    /// <exception cref="System.ArgumentNullException"/>
    /// <exception cref="HandlerNotFoundException"/>
    /// <exception cref="ResponseTypeMismatchException"/>
    IAsyncEnumerable<TItem> Stream<TItem>(
        IStreamRequest<TItem> request, CancellationToken cancellationToken = default);
}
