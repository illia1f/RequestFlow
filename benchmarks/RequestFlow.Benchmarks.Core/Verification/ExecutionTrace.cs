using System.Collections.Concurrent;

namespace RequestFlow.Benchmarks.Core;

public sealed class ExecutionTrace(int parallelHandlers = 0)
{
    private readonly ConcurrentQueue<ExecutionRecord> _entries = new();
    private readonly ConcurrentQueue<Task> _operations = new();
    private int _completedHolds;
    private readonly TaskCompletionSource? _handlerRelease = parallelHandlers > 0
        ? new(TaskCreationOptions.RunContinuationsAsynchronously) : null;

    public IReadOnlyList<ExecutionRecord> Entries => _entries.ToArray();

    public void Record(ExecutionRecord entry) => _entries.Enqueue(entry);

    public TTask Track<TTask>(TTask operation) where TTask : Task
    {
        _operations.Enqueue(operation);

        return operation;
    }

    public ValueTask Track(ValueTask operation) => new(Track(operation.AsTask()));

    public ValueTask<T> Track<T>(ValueTask<T> operation) => new(Track(operation.AsTask()));

    // Handlers are held only on suspending rows, so a held handler that already completed ignored its completion shape.
    internal Task HoldHandlers(Task operation)
        => _handlerRelease is null ? operation : WaitForRelease(Held(operation));

    internal ValueTask HoldHandlers(ValueTask operation)
        => _handlerRelease is null ? operation : new ValueTask(WaitForRelease(Held(operation.AsTask())));

    internal Task<T> HoldHandlers<T>(Task<T> operation)
        => _handlerRelease is null ? operation : WaitForRelease(Held(operation));

    internal ValueTask<T> HoldHandlers<T>(ValueTask<T> operation)
        => _handlerRelease is null ? operation : new ValueTask<T>(WaitForRelease(Held(operation.AsTask())));

    private TTask Held<TTask>(TTask operation) where TTask : Task
    {
        if (operation.IsCompleted)
            Interlocked.Increment(ref _completedHolds);

        return operation;
    }

    internal void VerifyFanOut()
    {
        if (_handlerRelease is null)
            return;

        ExecutionRecord[] entries = _entries.ToArray();
        int started = entries.Count(entry => TraceNames.IsEventHandlerStart(entry.Name));
        bool hasCompletedOperations = entries.Any(
            entry => entry.Name.EndsWith(TraceNames.CompletionSuffix, StringComparison.Ordinal));

        if (started != parallelHandlers || hasCompletedOperations)
            throw new InvalidOperationException($"Expected all {parallelHandlers} event handlers to start before completion was released; got {started} starts.");

        if (_completedHolds != 0)
            throw new InvalidOperationException($"{_completedHolds} held event handlers completed synchronously on a suspending row.");
    }

    internal void ReleaseHandlers() => _handlerRelease?.TrySetResult();

    private async Task WaitForRelease(Task operation)
    {
        await _handlerRelease!.Task.ConfigureAwait(false);
        await operation.ConfigureAwait(false);
    }

    private async Task<T> WaitForRelease<T>(Task<T> operation)
    {
        await _handlerRelease!.Task.ConfigureAwait(false);

        return await operation.ConfigureAwait(false);
    }

    internal void VerifyCompletion()
    {
        if (_operations.Any(operation => !operation.IsCompletedSuccessfully))
            throw new InvalidOperationException("The benchmark returned before its workload completed successfully.");

        int handlerStarts = _entries.Count(IsHandlerStart);
        int completedStreams = _entries.Count(entry => entry.Name == TraceNames.StreamComplete);
        int expectedOperations = handlerStarts - completedStreams;

        if (_operations.Count != expectedOperations)
            throw new InvalidOperationException("The benchmark did not complete one workload per handler.");
    }

    private static bool IsHandlerStart(ExecutionRecord entry)
        => entry.Name == TraceNames.Handler || TraceNames.IsEventHandlerStart(entry.Name);
}
