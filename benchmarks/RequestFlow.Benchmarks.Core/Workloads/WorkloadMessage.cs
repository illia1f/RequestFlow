namespace RequestFlow.Benchmarks.Core;

public class WorkloadMessage(CompletionKind completion = CompletionKind.Cached, int value = 42)
{
    public CompletionKind Completion { get; } = completion;

    public int Value { get; } = value;

    public ExecutionTrace? Trace { get; set; }
}
