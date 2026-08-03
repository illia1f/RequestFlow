using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Runs the stage chain by recursion: each level hands its stage the level below it. One
/// instance per dispatch, holding the request and the token the dispatch entered with, and
/// standing in for the level the outermost stage re-enters.
/// </summary>
internal abstract class StageExecutor<TRequest, TResponse> : IContinuation<TResponse>, IContinuation
    where TRequest : IRequest<TResponse>
{
    private readonly int _stageCount;
    private readonly TRequest _request;
    private readonly CancellationToken _cancellationToken;

    protected StageExecutor(int stageCount, TRequest request, CancellationToken cancellationToken)
    {
        _stageCount = stageCount;
        _request = request;
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    /// Runs the chain, starting at the outermost stage.
    /// </summary>
    public Task<TResponse> RunAsync()
        => _stageCount == 0
            ? StartHandlerAsync(_cancellationToken)
            : StartStageAsync(0, ResolveStage(0), this, _cancellationToken);

    /// <inheritdoc />
    public Task<TResponse> InvokeAsync(CancellationToken cancellationToken = default)
        => EnterBelowAsync(0, Inherit(cancellationToken, _cancellationToken));

    /// <inheritdoc />
    Task IContinuation.InvokeAsync(CancellationToken cancellationToken) => InvokeAsync(cancellationToken);

    /// <summary>
    /// The stage that runs at <paramref name="index"/>. Asked once per entry into that level.
    /// </summary>
    protected abstract object ResolveStage(int index);

    /// <summary>
    /// Invokes the stage at <paramref name="index"/> with <paramref name="next"/> as the rest
    /// of the chain.
    /// </summary>
    protected abstract Task<TResponse> InvokeStageAsync(
        int index, object stage, IContinuation<TResponse> next, TRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Invokes the handler. Reached only once the chain runs to the bottom, so a stage that
    /// short-circuits never resolves it.
    /// </summary>
    protected abstract Task<TResponse> InvokeHandlerAsync(TRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The type of the stage at <paramref name="index"/>, used to name it in errors.
    /// </summary>
    protected abstract Type StageTypeAt(int index);

    // None means the caller named no token, so the level's own carries on down.
    private static CancellationToken Inherit(CancellationToken supplied, CancellationToken current)
        => supplied == CancellationToken.None ? current : supplied;

    private Task<TResponse> StartStageAsync(
        int index, object stage, IContinuation<TResponse> next, CancellationToken cancellationToken)
    {
        Task<TResponse> task = InvokeStageAsync(index, stage, next, _request, cancellationToken);
        if (task is null)
            throw new StageNullTaskException(StageTypeAt(index));

        return task;
    }

    private Task<TResponse> StartHandlerAsync(CancellationToken cancellationToken)
        => NullTaskGuard.ThrowIfNull(InvokeHandlerAsync(_request, cancellationToken), typeof(TRequest));

    // Every call builds its own level below, so repeated and overlapping calls share no state.
    private Task<TResponse> EnterBelowAsync(int callerIndex, CancellationToken cancellationToken)
    {
        int index = callerIndex + 1;

        return index == _stageCount
            ? StartHandlerAsync(cancellationToken)
            : new Continuation(this, index, cancellationToken).EnterAsync();
    }

    // One level of one call, immutable once built: the token belongs to the call, not the level.
    private sealed class Continuation(
        StageExecutor<TRequest, TResponse> executor, int index, CancellationToken cancellationToken)
        : IContinuation<TResponse>, IContinuation
    {
        /// <inheritdoc />
        public Task<TResponse> InvokeAsync(CancellationToken suppliedToken = default)
            => executor.EnterBelowAsync(index, Inherit(suppliedToken, cancellationToken));

        /// <inheritdoc />
        Task IContinuation.InvokeAsync(CancellationToken suppliedToken) => InvokeAsync(suppliedToken);

        internal Task<TResponse> EnterAsync()
            => executor.StartStageAsync(index, executor.ResolveStage(index), this, cancellationToken);
    }
}
