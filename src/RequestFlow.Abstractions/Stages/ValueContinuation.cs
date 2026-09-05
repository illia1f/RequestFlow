using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// The rest of a value request stage chain below one stage.
/// </summary>
public readonly struct ValueContinuation<TResponse>
{
    private const string NotBuiltMessage =
        "This continuation is the default value of its type, so there is no chain below it to run. " +
        "A stage is handed its continuation by the dispatch; to build one in a test, call " +
        "ValueContinuation<TResponse>.Over(rest), passing a delegate that stands in for the rest of the chain.";

    private readonly ValueLevelEntry<TResponse> _below;
    private readonly object _request;
    private readonly IServiceProvider _services;
    private readonly CancellationToken _cancellationToken;

    internal ValueContinuation(
        ValueLevelEntry<TResponse> below,
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
    /// Builds a continuation for unit-testing a stage without a container.
    /// </summary>
    public static ValueContinuation<TResponse> Over(
        Func<CancellationToken, ValueTask<TResponse>> rest,
        CancellationToken cancellationToken = default)
    {
        if (rest is null)
            throw new ArgumentNullException(nameof(rest));

        return new ValueContinuation<TResponse>(
            (request, services, token) => rest(token),
            null!,
            null!,
            cancellationToken);
    }

    /// <summary>
    /// Runs the rest of the chain.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<TResponse> InvokeAsync(CancellationToken cancellationToken = default)
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
/// The void form of <see cref="ValueContinuation{TResponse}"/>.
/// </summary>
public readonly struct ValueContinuation
{
    private const string NotBuiltMessage =
        "This continuation is the default value of its type, so there is no chain below it to run. " +
        "A stage is handed its continuation by the dispatch; to build one in a test, call " +
        "ValueContinuation.Over(rest), passing a delegate that stands in for the rest of the chain.";

    private readonly ValueVoidLevelEntry _below;
    private readonly object _request;
    private readonly IServiceProvider _services;
    private readonly CancellationToken _cancellationToken;

    internal ValueContinuation(
        ValueVoidLevelEntry below,
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
    /// Builds a continuation for unit-testing a stage without a container.
    /// </summary>
    public static ValueContinuation Over(
        Func<CancellationToken, ValueTask> rest,
        CancellationToken cancellationToken = default)
    {
        if (rest is null)
            throw new ArgumentNullException(nameof(rest));

        return new ValueContinuation(
            (request, services, token) => rest(token),
            null!,
            null!,
            cancellationToken);
    }

    /// <summary>
    /// Runs the rest of the chain.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask InvokeAsync(CancellationToken cancellationToken = default)
    {
        if (_below is null)
            throw new InvalidOperationException(NotBuiltMessage);

        return _below(
            _request,
            _services,
            cancellationToken == CancellationToken.None ? _cancellationToken : cancellationToken);
    }
}
