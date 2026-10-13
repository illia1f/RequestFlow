namespace RequestFlow.Benchmarks.Core;

public static class BenchmarkWorkload
{
    public const int Input = 42;
    public const int Expected = Input + Increment;

    // What every response handler adds to its request value.
    private const int Increment = 37;

    private static readonly Task<int> CachedResponse = Task.FromResult(Expected);

    public static Task<int> TaskResponse(WorkloadMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        Task<int> operation = request.Completion switch
        {
            CompletionKind.Cached when request.Value == Input => CachedResponse,
            CompletionKind.Suspending => SuspendTask(request.Value, token),
            _ => Task.FromResult(request.Value + Increment),
        };

        return request.Trace is { } trace ? trace.Track(operation) : operation;
    }

    public static ValueTask<int> ValueResponse(WorkloadMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        ValueTask<int> operation = request.Completion == CompletionKind.Suspending
            ? SuspendValue(request.Value, token)
            : new ValueTask<int>(request.Value + Increment);

        return request.Trace is { } trace ? trace.Track(operation) : operation;
    }

    public static Task TaskVoidRequest(WorkloadMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        Task operation = request.Completion == CompletionKind.Suspending ? SuspendVoidRequest(token) : Task.CompletedTask;

        return request.Trace is { } trace ? trace.Track(trace.HoldHandlers(operation)) : operation;
    }

    public static ValueTask ValueVoidRequest(WorkloadMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        ValueTask operation = request.Completion == CompletionKind.Suspending ? SuspendValueVoidRequest(token) : default;

        return request.Trace is { } trace ? trace.Track(trace.HoldHandlers(operation)) : operation;
    }

    /// <summary>
    /// The <see cref="TaskVoidRequest"/> work for a library whose void handler returns a unit value.
    /// </summary>
    public static Task<TUnit> TaskUnitVoidRequest<TUnit>(WorkloadMessage request, CancellationToken token)
        where TUnit : struct
    {
        token.ThrowIfCancellationRequested();

        Task<TUnit> operation = request.Completion == CompletionKind.Suspending
            ? SuspendUnit<TUnit>(token)
            : CompletedUnit<TUnit>.Task;

        return request.Trace is { } trace ? trace.Track(trace.HoldHandlers(operation)) : operation;
    }

    /// <summary>
    /// The <see cref="ValueVoidRequest"/> work for a library whose void handler returns a unit value.
    /// </summary>
    public static ValueTask<TUnit> ValueUnitVoidRequest<TUnit>(WorkloadMessage request, CancellationToken token)
        where TUnit : struct
    {
        token.ThrowIfCancellationRequested();

        ValueTask<TUnit> operation = request.Completion == CompletionKind.Suspending
            ? SuspendValueUnit<TUnit>(token)
            : default;

        return request.Trace is { } trace ? trace.Track(trace.HoldHandlers(operation)) : operation;
    }

    public static async Task<int> Consume(IAsyncEnumerable<int> sequence)
    {
        int sum = 0;

        await foreach (int item in sequence.ConfigureAwait(false))
            sum += item;

        return sum;
    }

    public static async Task<T> AwaitTask<T>(Task<T> task) => await task.ConfigureAwait(false);

    public static async ValueTask<T> AwaitValue<T>(ValueTask<T> task) => await task.ConfigureAwait(false);

    public static async Task AwaitTask(Task task) => await task.ConfigureAwait(false);

    public static async ValueTask AwaitValue(ValueTask task) => await task.ConfigureAwait(false);

    private static async Task<int> SuspendTask(int value, CancellationToken token)
    {
        await Task.Yield();
        token.ThrowIfCancellationRequested();

        return value + Increment;
    }

    private static async ValueTask<int> SuspendValue(int value, CancellationToken token)
    {
        await Task.Yield();
        token.ThrowIfCancellationRequested();

        return value + Increment;
    }

    private static async Task SuspendVoidRequest(CancellationToken token)
    {
        await Task.Yield();
        token.ThrowIfCancellationRequested();
    }

    private static async ValueTask SuspendValueVoidRequest(CancellationToken token)
    {
        await Task.Yield();
        token.ThrowIfCancellationRequested();
    }

    private static async Task<TUnit> SuspendUnit<TUnit>(CancellationToken token)
        where TUnit : struct
    {
        await Task.Yield();
        token.ThrowIfCancellationRequested();

        return default;
    }

    private static async ValueTask<TUnit> SuspendValueUnit<TUnit>(CancellationToken token)
        where TUnit : struct
    {
        await Task.Yield();
        token.ThrowIfCancellationRequested();

        return default;
    }

    private static class CompletedUnit<TUnit>
        where TUnit : struct
    {
        public static readonly Task<TUnit> Task = System.Threading.Tasks.Task.FromResult(default(TUnit));
    }
}
