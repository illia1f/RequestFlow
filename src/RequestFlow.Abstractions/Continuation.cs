using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// The rest of the stage chain below one stage, ending at the request's handler. Invoke it again
/// to run that chain again, either after the first call finishes or while it is still running.
/// </summary>
/// <remarks>
/// Each call enters the levels below on its own and keeps the token it was given, so two
/// overlapping calls share no state of the library's. They do share whatever the container hands to
/// both: a transient stage below is built again per call, while a scoped or singleton one runs
/// inside both calls at once and has to be thread safe.
/// <para>
/// A level resolves before there is a task to hand back, so <see cref="InvokeAsync"/> can throw
/// instead of returning one. A stage holding a call it has not awaited owns that call, and has to
/// observe it when a later call or walk fails.
/// </para>
/// <para>
/// Only a dispatch builds one over a real chain. To run a stage on its own, build one with
/// <see cref="Over"/>; the default value of the type stands for no chain at all.
/// </para>
/// </remarks>
/// <typeparam name="TResponse">The response the chain produces.</typeparam>
public readonly struct Continuation<TResponse>
{
    // The void form wraps a Continuation<NoResult>, so its calls reach this same guard.
    private const string NotBuiltMessage =
        "This continuation is the default value of its type, so there is no chain below it to run. " +
        "A stage is handed its continuation by the dispatch; to build one in a test, call " +
        "Continuation<TResponse>.Over(rest), or Continuation.Over(rest) for a void request, passing a " +
        "delegate that stands in for the rest of the chain.";

    private readonly LevelEntry<TResponse> _below;
    private readonly object _request;
    private readonly IServiceProvider _services;
    private readonly CancellationToken _cancellationToken;

    internal Continuation(
        LevelEntry<TResponse> below,
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
    /// What the rest of the chain does. It is handed the token <see cref="InvokeAsync"/> was called
    /// with, or <paramref name="cancellationToken"/> when that call named none.
    /// </param>
    /// <param name="cancellationToken">
    /// The token to continue under when <see cref="InvokeAsync"/> is called without one, standing in
    /// for the token the stage under test received.
    /// </param>
    /// <exception cref="ArgumentNullException"/>
    public static Continuation<TResponse> Over(
        Func<CancellationToken, Task<TResponse>> rest, CancellationToken cancellationToken = default)
    {
        if (rest is null)
            throw new ArgumentNullException(nameof(rest));

        // A level resolves through the request and the provider; this one resolves nothing.
        return new Continuation<TResponse>(
            (request, services, token) => rest(token), null!, null!, cancellationToken);
    }

    /// <summary>
    /// Runs the rest of the chain.
    /// </summary>
    /// <param name="cancellationToken">
    /// The token every level below this stage runs under, the handler included. Omit it, or
    /// pass <see cref="CancellationToken.None"/>, to continue under the token this stage
    /// received.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// This continuation is the default value of its type, so it has no chain below it. Build one
    /// with <see cref="Over"/>.
    /// </exception>
    // Inlined so the guard and the token choice fold into the stage's call, leaving the delegate call.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<TResponse> InvokeAsync(CancellationToken cancellationToken = default)
    {
        if (_below is null)
            throw new InvalidOperationException(NotBuiltMessage);

        return _below(
            _request,
            _services,
            cancellationToken == CancellationToken.None ? _cancellationToken : cancellationToken);
    }
}

/// <summary>
/// The void form of <see cref="Continuation{TResponse}"/>, under the same rules.
/// </summary>
public readonly struct Continuation
{
    private readonly Continuation<NoResult> _inner;

    internal Continuation(Continuation<NoResult> inner) => _inner = inner;

    /// <summary>
    /// A continuation that runs <paramref name="rest"/> where the levels below a stage would be, for
    /// unit-testing a stage on its own without a container.
    /// </summary>
    /// <param name="rest">
    /// What the rest of the chain does. It is handed the token <see cref="InvokeAsync"/> was called
    /// with, or <paramref name="cancellationToken"/> when that call named none.
    /// </param>
    /// <param name="cancellationToken">
    /// The token to continue under when <see cref="InvokeAsync"/> is called without one, standing in
    /// for the token the stage under test received.
    /// </param>
    /// <exception cref="ArgumentNullException"/>
    public static Continuation Over(Func<CancellationToken, Task> rest, CancellationToken cancellationToken = default)
    {
        if (rest is null)
            throw new ArgumentNullException(nameof(rest));

        return new Continuation(Continuation<NoResult>.Over(
            token => Complete(rest(token)), cancellationToken));
    }

    /// <inheritdoc cref="Continuation{TResponse}.InvokeAsync"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task InvokeAsync(CancellationToken cancellationToken = default)
        => _inner.InvokeAsync(cancellationToken);

    private static Task<NoResult> Complete(Task rest)
        => rest as Task<NoResult> ?? AwaitAsync(rest);

    private static async Task<NoResult> AwaitAsync(Task rest)
    {
        await rest.ConfigureAwait(false);

        return NoResult.Value;
    }
}
