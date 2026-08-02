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

    // The outermost stage re-enters through the executor, so its next-call state sits here
    // rather than in a continuation of its own.
    private NextCall _next;

    protected StageExecutor(int stageCount, TRequest request, CancellationToken cancellationToken)
    {
        _stageCount = stageCount;
        _request = request;
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    /// Runs the chain, starting at the outermost stage.
    /// </summary>
    // Nothing re-enters the outermost level, so its stage needs no slot to be held in between.
    public Task<TResponse> RunAsync()
        => _stageCount == 0
            ? StartHandlerAsync(_cancellationToken)
            : StartStageAsync(0, ResolveStage(0), this, _cancellationToken);

    /// <inheritdoc />
    public Task<TResponse> InvokeAsync(CancellationToken cancellationToken = default)
        => EnterBelowAsync(0, ref _next, Inherit(cancellationToken, _cancellationToken));

    /// <inheritdoc />
    Task IContinuation.InvokeAsync(CancellationToken cancellationToken) => InvokeAsync(cancellationToken);

    /// <summary>
    /// The stage that runs at <paramref name="index"/>. Each level asks once per dispatch and
    /// keeps the answer.
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
    /// short-circuits never builds it; the instance is kept so a stage that calls next again
    /// reuses it.
    /// </summary>
    protected abstract Task<TResponse> InvokeHandlerAsync(TRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The type of the stage at <paramref name="index"/>, used to name it in errors.
    /// </summary>
    protected abstract Type StageTypeAt(int index);

    // A call that names no token continues under the one its own level received, so a token a
    // stage above substituted stays in force for every level under it.
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

    /// <summary>
    /// Enters the level under the stage at <paramref name="callerIndex"/>, under the guard in
    /// <paramref name="slot"/>, which belongs to that stage's calls.
    /// </summary>
    private Task<TResponse> EnterBelowAsync(int callerIndex, ref NextCall slot, CancellationToken cancellationToken)
    {
        if (!TryClaim(ref slot.InFlight, out Task? prior))
            throw new OverlappingNextCallException(StageTypeAt(callerIndex));

        try
        {
            Task<TResponse> task = StartBelowAsync(callerIndex + 1, ref slot.Below, cancellationToken);
            Volatile.Write(ref slot.InFlight, task);
            return task;
        }
        catch
        {
            // A synchronous throw releases the claim so an outer retry stage may call next again.
            Volatile.Write(ref slot.InFlight, prior);
            throw;
        }
    }

    private Task<TResponse> StartBelowAsync(int index, ref Continuation? below, CancellationToken cancellationToken)
        => index == _stageCount
            ? StartHandlerAsync(cancellationToken)
            : (below ??= new Continuation(this, index)).EnterAsync(cancellationToken);

    // The compare-exchange makes the check and the claim one atomic step; of two simultaneous
    // callers, the loser sees either the sentinel or a value that moved.
    private static bool TryClaim(ref Task? guard, out Task? prior)
    {
        prior = Volatile.Read(ref guard);
        return prior is not { IsCompleted: false }
            && Interlocked.CompareExchange(ref guard, GuardSentinel.Claimed, prior) == prior;
    }

    /// <summary>
    /// One stage's next-call state: the guard that admits one call at a time, and the level its
    /// calls enter.
    /// </summary>
    // Always reached by ref. A copy would guard storage nobody reads.
    private struct NextCall
    {
        public Task? InFlight;
        public Continuation? Below;
    }

    // Which level a next call enters has to live in the object the call reaches: the moment a
    // stage suspends, shared executor state stops saying which frame is calling. Each level
    // below the outermost therefore gets its own continuation, holding its stage, its guard
    // state, and the token its stage is running under.
    private sealed class Continuation(StageExecutor<TRequest, TResponse> executor, int index)
        : IContinuation<TResponse>, IContinuation
    {
        private object? _stage;
        private NextCall _next;
        private CancellationToken _cancellationToken;

        /// <inheritdoc />
        public Task<TResponse> InvokeAsync(CancellationToken cancellationToken = default)
            => executor.EnterBelowAsync(index, ref _next, Inherit(cancellationToken, _cancellationToken));

        /// <inheritdoc />
        Task IContinuation.InvokeAsync(CancellationToken cancellationToken) => InvokeAsync(cancellationToken);

        // The stage above may call next again, so the level keeps the stage it resolved the
        // first time and every later pass runs that same instance. The guard admits one call at
        // a time, which is also what publishes the slot to the next caller's thread. The token
        // is stored before the stage runs, so a call the stage makes without one reads the token
        // this pass was entered with.
        internal Task<TResponse> EnterAsync(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;

            return executor.StartStageAsync(
                index, _stage ??= executor.ResolveStage(index), this, cancellationToken);
        }
    }
}
