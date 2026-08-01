using System;
using System.Threading;
using System.Threading.Tasks;

namespace RequestFlow;

/// <summary>
/// Runs the stage chain by recursion: each level hands its stage a continuation that enters
/// the level below. One instance per dispatch, holding the request and token for every level.
/// </summary>
internal abstract class StageExecutor<TRequest, TResponse>
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
    public Task<TResponse> RunAsync() => EnterAsync(0);

    protected abstract Task<TResponse> InvokeStageAsync(
        int index, TRequest request, StageDelegate<TResponse> next, CancellationToken cancellationToken);

    protected abstract Task<TResponse> InvokeHandlerAsync(TRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The runtime type of the stage at <paramref name="index"/>, used to name it in errors.
    /// </summary>
    protected abstract Type StageTypeAt(int index);

    private Task<TResponse> EnterAsync(int index)
    {
        Task<TResponse> task = index < _stageCount
            ? InvokeStageAsync(index, _request, new Continuation(this, index + 1).InvokeAsync, _cancellationToken)
            : InvokeHandlerAsync(_request, _cancellationToken);

        if (task is null)
            throw new InvalidOperationException(NullTaskMessage(index));

        return task;
    }

    // Which level a next call enters has to live in the delegate itself: the moment a stage
    // suspends on anything, shared executor state stops saying which frame is calling. Each
    // level therefore gets its own continuation, which also carries the state that catches a
    // stage calling next again while its earlier call is still running.
    private sealed class Continuation(StageExecutor<TRequest, TResponse> executor, int index)
    {
        // Never completes, so callers that read it while a claim is held treat it as a call
        // still in flight and throw.
        private static readonly Task<TResponse> Claimed = new TaskCompletionSource<TResponse>().Task;

        private Task<TResponse>? _running;

        public Task<TResponse> InvokeAsync()
        {
            // The compare-exchange makes the check and the claim one atomic step; of two
            // simultaneous callers, the loser sees either the sentinel or a value that moved.
            Task<TResponse>? running = Volatile.Read(ref _running);
            if (running is { IsCompleted: false }
                || Interlocked.CompareExchange(ref _running, Claimed, running) != running)
                throw new InvalidOperationException(executor.OverlappingNextMessage(index - 1));

            try
            {
                Task<TResponse> task = executor.EnterAsync(index);
                Volatile.Write(ref _running, task);
                return task;
            }
            catch
            {
                // A synchronous throw releases the claim so an outer retry stage may call next again.
                Volatile.Write(ref _running, running);
                throw;
            }
        }
    }

    private string OverlappingNextMessage(int caller)
        => $"Stage '{StageTypeAt(caller).FullName}' called next while the task from its earlier call was still running. " +
           "Await that task before calling next again: each call runs the rest of the chain, so overlapping calls would run it twice at once.";

    private string NullTaskMessage(int index)
        => index < _stageCount
            ? $"Stage '{StageTypeAt(index).FullName}' returned a null task from HandleAsync; " +
              "return the task from next, or a completed task when short-circuiting."
            : NullTaskGuard.HandlerMessage(typeof(TRequest));
}
