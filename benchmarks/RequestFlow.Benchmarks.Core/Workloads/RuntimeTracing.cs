using System.Runtime.CompilerServices;

namespace RequestFlow.Benchmarks.Core;

public static class RuntimeTracing
{
    public static Task EventTask(
        WorkloadMessage message, WorkloadComponent component, string name, CancellationToken token)
    {
        Task operation = BenchmarkWorkload.TaskVoidRequest(message, token);

        return message.Trace is null ? operation : CompleteTask(operation, message.Trace, component, name, token);
    }

    public static ValueTask EventValue(
        WorkloadMessage message, WorkloadComponent component, string name, CancellationToken token)
    {
        ValueTask operation = BenchmarkWorkload.ValueVoidRequest(message, token);

        return message.Trace is null ? operation : CompleteValue(operation, message.Trace, component, name, token);
    }

    public static async IAsyncEnumerable<int> Stream(
        WorkloadMessage message, int count, WorkloadComponent component,
        [EnumeratorCancellation] CancellationToken token)
    {
        bool suspending = message.Completion == CompletionKind.Suspending;

        try
        {
            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();

                if (suspending)
                    await Task.Yield();

                message.Trace?.Record(new ExecutionRecord(TraceNames.ItemPrefix + i, component, component.Scope, token));
                yield return i;
            }

            message.Trace?.Record(new ExecutionRecord(TraceNames.StreamComplete, component, component.Scope, token));
        }
        finally
        {
            message.Trace?.Record(new ExecutionRecord(TraceNames.StreamDispose, component, component.Scope, token));
        }
    }

    private static async Task CompleteTask(
        Task operation, ExecutionTrace trace, WorkloadComponent component, string name, CancellationToken token)
    {
        await operation.ConfigureAwait(false);
        trace.Record(new ExecutionRecord(name + TraceNames.CompletionSuffix, component, component.Scope, token));
    }

    private static async ValueTask CompleteValue(
        ValueTask operation, ExecutionTrace trace, WorkloadComponent component, string name, CancellationToken token)
    {
        await operation.ConfigureAwait(false);
        trace.Record(new ExecutionRecord(name + TraceNames.CompletionSuffix, component, component.Scope, token));
    }
}
