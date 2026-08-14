using System.Collections.Generic;
using System.Threading;

namespace RequestFlow;

/// <summary>
/// Produces the sequence one stream request asks for.
/// </summary>
/// <typeparam name="TRequest">The stream request this handler serves.</typeparam>
/// <typeparam name="TItem">The type each element of the sequence has.</typeparam>
public interface IStreamRequestHandler<in TRequest, TItem>
    where TRequest : IStreamRequest<TItem>
{
    /// <summary>
    /// Produces the sequence for <paramref name="request"/>. The dispatch passes
    /// <paramref name="cancellationToken"/> directly, so use it as it arrives.
    /// </summary>
    /// <remarks>
    /// An implementation written as an async iterator decorates its token parameter with
    /// <c>[EnumeratorCancellation]</c>, which is what the compiler asks for and costs nothing here:
    /// the dispatcher has already joined the token it hands over with the one the enumeration was given.
    /// </remarks>
    IAsyncEnumerable<TItem> Handle(TRequest request, CancellationToken cancellationToken);
}
