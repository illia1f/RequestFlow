namespace RequestFlow.Benchmarks.Core;

// Holds continuations posted during a probe call, so a suspending operation cannot finish before the probe reads its state.
// A call that blocks on its own deferred continuation is released after the deadline and fails the probe.
internal sealed class DeferredContinuations : SynchronizationContext, IDisposable
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(1);

    private readonly SynchronizationContext? _previous = Current;
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _pending = new();
    private readonly Timer _deadline;
    private bool _released;

    public bool DeadlineExpired { get; private set; }

    public DeferredContinuations()
    {
        _deadline = new Timer(_ => Expire(), null, Timeout.Infinite, Timeout.Infinite);
        SetSynchronizationContext(this);
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        lock (_pending)
        {
            if (!_released)
            {
                _pending.Enqueue((d, state));
                _deadline.Change(Deadline, Timeout.InfiniteTimeSpan);

                return;
            }
        }

        Forward((d, state));
    }

    public override SynchronizationContext CreateCopy() => this;

    public void Dispose()
    {
        SetSynchronizationContext(_previous);
        Release();
        _deadline.Dispose();
    }

    private void Expire()
    {
        DeadlineExpired = true;
        Release();
    }

    private void Release()
    {
        (SendOrPostCallback Callback, object? State)[] pending;

        lock (_pending)
        {
            _released = true;
            pending = _pending.ToArray();
            _pending.Clear();
        }

        foreach ((SendOrPostCallback Callback, object? State) item in pending)
            Forward(item);
    }

    private static void Forward((SendOrPostCallback Callback, object? State) item)
        => ThreadPool.QueueUserWorkItem(static item => item.Callback(item.State), item, preferLocal: false);
}
