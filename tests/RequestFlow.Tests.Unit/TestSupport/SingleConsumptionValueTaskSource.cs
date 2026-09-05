using System.Threading.Tasks.Sources;

namespace RequestFlow.Tests.Unit;

internal sealed class SingleConsumptionValueTaskSource<T> : IValueTaskSource<T>
{
    private ManualResetValueTaskSourceCore<T> _core;
    private int _getResultCalls;

    public int GetResultCalls => _getResultCalls;

    public ValueTask<T> CreateValueTask()
        => new(this, _core.Version);

    public void SetResult(T result)
        => _core.SetResult(result);

    public T GetResult(short token)
    {
        if (Interlocked.Increment(ref _getResultCalls) != 1)
            throw new InvalidOperationException("The ValueTask source was consumed more than once.");

        return _core.GetResult(token);
    }

    public ValueTaskSourceStatus GetStatus(short token)
        => _core.GetStatus(token);

    public void OnCompleted(
        Action<object?> continuation,
        object? state,
        short token,
        ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);
}

internal sealed class SingleConsumptionValueTaskSource : IValueTaskSource
{
    private ManualResetValueTaskSourceCore<bool> _core;
    private int _getResultCalls;

    public int GetResultCalls => _getResultCalls;

    public ValueTask CreateValueTask()
        => new(this, _core.Version);

    public void SetResult()
        => _core.SetResult(true);

    public void GetResult(short token)
    {
        if (Interlocked.Increment(ref _getResultCalls) != 1)
            throw new InvalidOperationException("The ValueTask source was consumed more than once.");

        _core.GetResult(token);
    }

    public ValueTaskSourceStatus GetStatus(short token)
        => _core.GetStatus(token);

    public void OnCompleted(
        Action<object?> continuation,
        object? state,
        short token,
        ValueTaskSourceOnCompletedFlags flags)
        => _core.OnCompleted(continuation, state, token, flags);
}
