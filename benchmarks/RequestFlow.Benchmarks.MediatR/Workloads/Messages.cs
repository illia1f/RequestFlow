using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.MediatR;

public sealed class MediatRTaskRequest(CompletionKind completion = CompletionKind.Cached, int value = 42)
    : WorkloadMessage(completion, value), global::MediatR.IRequest<int>;

public sealed class MediatRVoidRequest(CompletionKind completion = CompletionKind.Cached)
    : WorkloadMessage(completion), global::MediatR.IRequest;

public sealed class MediatRStreamRequest(int count = 10, CompletionKind completion = CompletionKind.Synchronous)
    : WorkloadMessage(completion), global::MediatR.IStreamRequest<int>
{
    public int Count { get; } = count;
}

public sealed class MediatREvent(CompletionKind completion = CompletionKind.Cached)
    : WorkloadMessage(completion), global::MediatR.INotification;
