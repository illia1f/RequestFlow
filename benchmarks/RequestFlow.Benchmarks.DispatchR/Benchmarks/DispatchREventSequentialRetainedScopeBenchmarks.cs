using BenchmarkDotNet.Attributes;
using RequestFlow.Benchmarks.Core;
using RequestFlow.Benchmarks.RequestFlowFixtures;

namespace RequestFlow.Benchmarks.DispatchR;

[BenchmarkScope(ScopeLifetime.RetainedScope)]
public class DispatchREventSequentialRetainedScopeBenchmarks
{
    [Params(CompletionKind.Synchronous, CompletionKind.Suspending)]
    public CompletionKind Completion { get; set; }

    [Params(1, 5)]
    public int HandlerCount { get; set; }

    private RetainedScopeHost<global::RequestFlow.IEventPublisher> _requestFlow = null!;
    private RetainedScopeHost<global::DispatchR.IMediator> _dispatchR = null!;

    private EventMessage _requestFlowRequest = null!;
    private DispatchREvent _dispatchRRequest = null!;

    [GlobalSetup]
    public void Setup()
    {
        _requestFlow = new(RequestFlowSetup.Build(RequestFlowSetup.EventServices(HandlerCount, parallel: false)));
        _dispatchR = new(DispatchRSetup.Build(DispatchRSetup.EventServices(HandlerCount)));

        _requestFlowRequest = new EventMessage(Completion);
        _dispatchRRequest = new DispatchREvent(Completion);

        RequestFlow().GetAwaiter().GetResult();
        DispatchR().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public Task RequestFlow()
        => _requestFlow.Service.PublishAsync(_requestFlowRequest, CancellationToken.None);

    [Benchmark]
    public ValueTask DispatchR()
        => _dispatchR.Service.Publish(_dispatchRRequest, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _requestFlow.Dispose();
        _dispatchR.Dispose();
    }
}
