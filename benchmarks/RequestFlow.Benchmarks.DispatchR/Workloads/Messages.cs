using RequestFlow.Benchmarks.Core;

namespace RequestFlow.Benchmarks.DispatchR;

public sealed class DispatchRTaskRequest(CompletionKind completion = CompletionKind.Cached, int value = 42)
    : WorkloadMessage(completion, value), global::DispatchR.Abstractions.Send.IRequest<DispatchRTaskRequest, Task<int>>;

public sealed class DispatchRValueRequest(CompletionKind completion = CompletionKind.Cached, int value = 42)
    : WorkloadMessage(completion, value), global::DispatchR.Abstractions.Send.IRequest<DispatchRValueRequest, ValueTask<int>>;

public sealed class DispatchRVoidRequest(CompletionKind completion = CompletionKind.Cached)
    : WorkloadMessage(completion), global::DispatchR.Abstractions.Send.IRequest<DispatchRVoidRequest, Task<DispatchRVoidRequestResult>>;

public sealed class DispatchRValueVoidRequest(CompletionKind completion = CompletionKind.Cached)
    : WorkloadMessage(completion), global::DispatchR.Abstractions.Send.IRequest<DispatchRValueVoidRequest, ValueTask<DispatchRVoidRequestResult>>;

public readonly record struct DispatchRVoidRequestResult;

public sealed class DispatchRStreamRequest(int count = 10, CompletionKind completion = CompletionKind.Synchronous)
    : WorkloadMessage(completion), global::DispatchR.Abstractions.Stream.IStreamRequest<DispatchRStreamRequest, int>
{
    public int Count { get; } = count;
}

public sealed class DispatchREvent(CompletionKind completion = CompletionKind.Cached)
    : WorkloadMessage(completion), global::DispatchR.Abstractions.Notification.INotification;
