using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace RequestFlow;

/// <summary>
/// The rest of the stream chain below one stage, ending at the request's handler. Invoke it again
/// to run that chain again, and enumerate what one call returns as many times as the levels below allow.
/// </summary>
/// <remarks>
/// Each call enters the levels below on its own and keeps the token it was given, so two
/// overlapping calls share no state of the library's. They do share whatever the container hands to
/// both: a transient stage below is built again per call, while a scoped or singleton one runs
/// inside both calls at once and has to be thread safe.
/// <para>
/// <see cref="Invoke"/> enters the level below on the call: it resolves the next stage or the
/// handler and calls its Handle. A stage that invokes and drops what comes back has already run
/// that much. Items arrive later, because a level written as an async iterator holds its body
/// until the sequence is enumerated.
/// </para>
/// <para>
/// So <see cref="Invoke"/> throws whatever entering that level throws: the guard below on a
/// continuation that was never built over a chain, a resolution failure, a
/// <see cref="NullStreamException"/> for a level that handed back null, or anything a level that
/// is not an iterator throws as it starts.
/// </para>
/// <para>
/// Only a dispatch builds one over a real chain. To run a stage on its own, build one with
/// <see cref="Over"/>; the default value of the type stands for no chain at all.
/// </para>
/// </remarks>
/// <typeparam name="TItem">The type each element of the sequence has.</typeparam>
public readonly struct StreamContinuation<TItem>
{
    private const string NotBuiltMessage =
        "This continuation is the default value of its type, so there is no chain below it to run. " +
        "A stage is handed its continuation by the dispatch; to build one in a test, call " +
        "StreamContinuation<TItem>.Over(rest), passing a delegate that stands in for the rest of " +
        "the chain.";

    private readonly StreamLevelEntry<TItem> _below;
    private readonly object _request;
    private readonly IServiceProvider _services;
    private readonly CancellationToken _cancellationToken;

    internal StreamContinuation(
        StreamLevelEntry<TItem> below,
        object request,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        _below = below;
        _request = request;
        _services = services;
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    /// A continuation that runs <paramref name="rest"/> where the levels below a stage would be, for
    /// unit-testing a stage on its own without a container.
    /// </summary>
    /// <param name="rest">
    /// What the rest of the chain does. It is handed the token <see cref="Invoke"/> was called with,
    /// or <paramref name="cancellationToken"/> when that call named none.
    /// </param>
    /// <param name="cancellationToken">
    /// The token to continue under when <see cref="Invoke"/> is called without one, standing in for
    /// the token the stage under test received.
    /// </param>
    /// <exception cref="ArgumentNullException"/>
    public static StreamContinuation<TItem> Over(
        Func<CancellationToken, IAsyncEnumerable<TItem>> rest, CancellationToken cancellationToken = default)
    {
        if (rest is null)
            throw new ArgumentNullException(nameof(rest));

        // A level resolves through the request and the provider; this one resolves nothing.
        return new StreamContinuation<TItem>(
            (request, services, token) => rest(token), null!, null!, cancellationToken);
    }

    /// <summary>
    /// Runs the rest of the chain.
    /// </summary>
    /// <param name="cancellationToken">
    /// The token every level below this stage runs under, the handler included. Omit it, or pass
    /// <see cref="CancellationToken.None"/>, to continue under the token this stage received.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// This continuation is the default value of its type, so it has no chain below it.
    /// Build one with <see cref="Over"/>.
    /// </exception>
    // Inlined so the guard and the token choice fold into the stage's call, leaving the delegate call.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IAsyncEnumerable<TItem> Invoke(CancellationToken cancellationToken = default)
    {
        if (_below is null)
            throw new InvalidOperationException(NotBuiltMessage);

        return _below(
            _request,
            _services,
            cancellationToken == CancellationToken.None ? _cancellationToken : cancellationToken);
    }
}
